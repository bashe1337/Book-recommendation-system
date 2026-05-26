"""ALS (Alternating Least Squares) для implicit feedback (Hu et al., 2008).

Алгоритм:
  • Факторизуем user-item матрицу confidence как R ≈ P · Qᵀ, где P — латентные
    факторы пользователей (n_users × k), Q — латентные факторы items (n_items × k).
  • Confidence c_ui = 1 + α · raw_signal — чем сильнее сигнал, тем сильнее модель
    "верит" в эту пару. preference p_ui = 1, если сигнал > 0, иначе 0.
  • Минимизируем взвешенную сумму квадратов отклонений с регуляризацией.
    ALS чередует фиксацию P и Q, на каждом шаге задача становится квадратичной
    и решается в замкнутой форме — этим и обусловлено название.

Зачем ALS поверх user/item-based CF:
  • Латентные факторы восстанавливают "пустые клетки" матрицы → справляется
    с разреженностью лучше memory-based.
  • Естественно объединяет неявные сигналы (просмотры, избранное) и явные
    (оценки) в одной модели через confidence веса.
  • Линейная сложность: фиксированное обучение, инференс — два dot product.

Под капотом используем библиотеку `implicit` (B. Frederickson) — оптимизированную
на Cython реализацию ALS, в 50–200x быстрее наивного numpy.
"""

import logging
import time
from contextlib import contextmanager
from dataclasses import dataclass

import numpy as np
import pandas as pd
from implicit.als import AlternatingLeastSquares
from scipy.sparse import csr_matrix

from models.collaborative import filter_ratings

logger = logging.getLogger(__name__)


@contextmanager
def _step(name: str):
    """Логирует начало этапа и его длительность."""
    logger.info("[ALS] >>> %s ...", name)
    t0 = time.perf_counter()
    yield
    logger.info("[ALS] <<< %s — %.2f c", name, time.perf_counter() - t0)


@dataclass
class _Index:
    """Соответствие исходных id и позиций в матрице."""
    user_ids: list
    book_ids: list
    user_to_idx: dict
    book_to_idx: dict


class ALSModel:
    """Implicit-feedback ALS с обёрткой над `implicit.AlternatingLeastSquares`.

    Принимает DataFrame с колонками (user_id, book_id, confidence).
    confidence — суммарный "сигнал интереса" пользователя к книге (просмотры,
    избранное, оценки агрегируются снаружи через `load_all_interactions`).
    """

    def __init__(
        self,
        factors: int = 64,
        regularization: float = 0.01,
        alpha: float = 15.0,
        iterations: int = 15,
        min_user_interactions: int = 3,
        min_book_interactions: int = 3,
        random_state: int = 42,
        use_gpu: bool = False,
    ):
        # Гиперпараметры — дефолты близки к рекомендациям Hu et al. для среднего датасета.
        self.factors = factors                 # размерность латентного пространства
        self.regularization = regularization   # L2-регуляризация (предотвращает переобучение)
        self.alpha = alpha                     # множитель уверенности: c = 1 + α·signal
        self.iterations = iterations           # сколько чередований P↔Q сделать
        # Фильтры активности (мягче чем для memory-based — ALS лучше работает с холодными)
        self.min_user_interactions = min_user_interactions
        self.min_book_interactions = min_book_interactions
        self.random_state = random_state
        self.use_gpu = use_gpu

        self._als: AlternatingLeastSquares | None = None
        # Сырая матрица user × item с весами confidence. Нужна для
        # filter_already_liked_items=True при выдаче рекомендаций.
        self._user_items: csr_matrix | None = None
        self._index: _Index | None = None

    @property
    def is_fitted(self) -> bool:
        return self._als is not None

    @property
    def stats(self) -> dict:
        if not self.is_fitted:
            return {"fitted": False}
        return {
            "fitted": True,
            "users": len(self._index.user_ids),
            "books": len(self._index.book_ids),
            "interactions": int(self._user_items.nnz),
            "factors": self.factors,
            "regularization": self.regularization,
            "alpha": self.alpha,
            "iterations": self.iterations,
        }

    # ------------------------------------------------------------------ fit
    def fit(self, interactions_df: pd.DataFrame, prefiltered: bool = False) -> None:
        """Обучение ALS.

        interactions_df: колонки user_id, book_id, confidence (или rating —
        тогда воспринимается как явная оценка с тем же весом).
        prefiltered=True — пропустить фильтр (нужно при правильном pipeline
        filter → split → fit, иначе после split часть юзеров не пройдёт порог).
        """
        fit_started = time.perf_counter()

        # Унификация имени value-колонки.
        df = interactions_df.copy()
        if "confidence" not in df.columns and "rating" in df.columns:
            df = df.rename(columns={"rating": "confidence"})
        if "confidence" not in df.columns:
            raise ValueError("interactions_df должен содержать колонку confidence или rating")

        logger.info("[ALS] fit: вход — %d взаимодействий (prefiltered=%s)",
                    len(df), prefiltered)

        if not prefiltered:
            with _step("filter"):
                df = filter_ratings(df, self.min_user_interactions, self.min_book_interactions)
        else:
            logger.info("[ALS] >>> filter SKIPPED (данные уже отфильтрованы)")

        if df.empty:
            raise ValueError("Нет взаимодействий для обучения — снизьте min_*_interactions")

        with _step("build sparse matrix"):
            users_cat = pd.Categorical(df["user_id"])
            books_cat = pd.Categorical(df["book_id"])
            n_users = len(users_cat.categories)
            n_books = len(books_cat.categories)

            logger.info("[ALS] размеры: users=%d, books=%d, interactions=%d, "
                        "sparsity=%.4f%%",
                        n_users, n_books, len(df),
                        100.0 * len(df) / (n_users * n_books))

            # confidence как float — ALS внутри сделает c = 1 + α · value.
            user_items = csr_matrix(
                (df["confidence"].astype(float).values,
                 (users_cat.codes, books_cat.codes)),
                shape=(n_users, n_books),
            )

        with _step(f"ALS fit (factors={self.factors}, "
                   f"iter={self.iterations}, alpha={self.alpha})"):
            self._als = AlternatingLeastSquares(
                factors=self.factors,
                regularization=self.regularization,
                alpha=self.alpha,
                iterations=self.iterations,
                random_state=self.random_state,
                use_gpu=self.use_gpu,
                # причина: implicit любит спамить tqdm-полосками. Отключаем,
                # чтобы логи не выглядели как мусор — у нас уже есть _step таймер.
                calculate_training_loss=False,
            )
            # implicit ≥ 0.5: fit принимает user×item матрицу с raw values;
            # confidence c = 1 + alpha·value считается внутри.
            self._als.fit(user_items)

        with _step("save artifacts"):
            self._user_items = user_items
            self._index = _Index(
                user_ids=list(users_cat.categories),
                book_ids=list(books_cat.categories),
                user_to_idx={uid: i for i, uid in enumerate(users_cat.categories)},
                book_to_idx={bid: i for i, bid in enumerate(books_cat.categories)},
            )

        logger.info("[ALS] fit: ВСЁ ГОТОВО за %.2f c", time.perf_counter() - fit_started)

    # -------------------------------------------------------------- predict
    def predict_all_for_user(self, user_idx: int) -> np.ndarray:
        """Сырые score'ы для всех книг = user_factors[u] · item_factors.T.

        Это не оценка в шкале 1..5 — это латентное "сродство" пользователя
        и книги. Используется для расчёта Precision@K / Recall@K / NDCG@K.
        Чем выше score, тем сильнее модель рекомендует книгу.
        """
        # implicit ≥ 0.6 хранит user_factors как np.ndarray
        u_vec = self._als.user_factors[user_idx]
        return self._als.item_factors @ u_vec

    def recommend(self, user_id, n: int = 10, filter_seen: bool = True) -> list[dict]:
        """Топ-n книг для пользователя.

        filter_seen=True — исключить книги, с которыми пользователь уже
        взаимодействовал (стандартно для production).
        """
        if not self.is_fitted:
            return []
        u_idx = self._index.user_to_idx.get(user_id)
        if u_idx is None:
            return []

        # implicit.recommend возвращает (ids, scores) — индексы книг и score'ы.
        ids, scores = self._als.recommend(
            u_idx,
            self._user_items[u_idx],
            N=n,
            filter_already_liked_items=filter_seen,
        )

        return [
            {"book_id": self._index.book_ids[int(i)], "score": float(s)}
            for i, s in zip(ids, scores)
        ]
