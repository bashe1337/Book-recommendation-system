"""Item-based Collaborative Filtering на adjusted cosine similarity.

Алгоритм (Sarwar et al., 2001 — каноническая статья):
  1) Строим разреженную матрицу user × item.
  2) **Adjusted cosine**: вычитаем среднюю оценку пользователя ("его шкалу") —
     это снимает рейтинговое предубеждение оптимистов/строгих критиков.
  3) Считаем item-item cosine similarity по **столбцам** mean-centered матрицы.
  4) Для каждого item оставляем top-k наиболее похожих (по модулю) — это режет
     память и убирает шум от случайных слабых корреляций.
  5) Прогноз = взвешенное среднее **сырых** оценок пользователя на items,
     похожих на целевой:
       pred(u, i) = Σ_j sim(i, j) * r(u, j) / Σ_j |sim(i, j)|
     где j пробегает по items, оценённым пользователем u и попавшим в top-k
     соседей item i.

Зачем item-based когда есть user-based:
  • items меняются медленнее, чем users → similarity дольше остаётся актуальной
    (модель реже нужно переобучать);
  • в каталоге обычно меньше items, чем users → матрица меньше → быстрее;
  • рекомендации легче объяснить: "вам понравилось X, поэтому советуем Y".
"""

import logging
import time
from contextlib import contextmanager
from dataclasses import dataclass

import numpy as np
import pandas as pd
from scipy.sparse import csr_matrix
from sklearn.metrics.pairwise import cosine_similarity

from models.collaborative import filter_ratings

logger = logging.getLogger(__name__)

# Item × item матрица занимает n_items^2 * 8 байт. При 15K items это ~1.8 GB.
_MAX_ITEMS_DENSE = 15000


@contextmanager
def _step(name: str):
    """Логирует начало этапа и его длительность — для прозрачности обучения."""
    logger.info("[IB-CF] >>> %s ...", name)
    t0 = time.perf_counter()
    yield
    logger.info("[IB-CF] <<< %s — %.2f c", name, time.perf_counter() - t0)


@dataclass
class _Index:
    """Соответствие исходных id и позиций в матрице."""
    user_ids: list
    book_ids: list
    user_to_idx: dict
    book_to_idx: dict


class ItemBasedCFModel:
    """Item-based CF с top-k разреживанием матрицы сходства."""

    def __init__(
        self,
        k_neighbors: int = 30,
        min_user_ratings: int = 10,
        min_book_ratings: int = 20,
    ):
        # k — сколько похожих items оставляем в каждой строке similarity matrix.
        self.k = k_neighbors
        # Минимумы по активности — без фильтрации сходство items с 1-2
        # общими оценками будет случайным шумом.
        self.min_user_ratings = min_user_ratings
        self.min_book_ratings = min_book_ratings

        self._raw: csr_matrix | None = None             # user × item, сырые оценки
        self._centered: csr_matrix | None = None        # user × item, mean-centered
        self._user_means: np.ndarray | None = None
        # Транспонированные сразу при fit: matmul (1, n_items) @ similarity_T идёт
        # через CSR×CSR — быстро. Хранение лишней копии оправдано производительностью.
        self._similarity_T: csr_matrix | None = None    # similarity.T, top-k по строкам
        self._abs_similarity_T: csr_matrix | None = None
        self._similarity: csr_matrix | None = None      # для diagnostics/stats
        self._index: _Index | None = None

    @property
    def is_fitted(self) -> bool:
        return self._similarity_T is not None

    @property
    def stats(self) -> dict:
        if not self.is_fitted:
            return {"fitted": False}
        return {
            "fitted": True,
            "users": len(self._index.user_ids),
            "books": len(self._index.book_ids),
            "ratings": int(self._raw.nnz),
            "k_neighbors": self.k,
            "min_user_ratings": self.min_user_ratings,
            "min_book_ratings": self.min_book_ratings,
            "similarity_nnz": int(self._similarity.nnz),
        }

    # ------------------------------------------------------------------ fit
    def fit(self, ratings_df: pd.DataFrame, prefiltered: bool = False) -> None:
        """Обучение item-based CF.

        prefiltered=True — данные уже отфильтрованы снаружи (filter_ratings
        перед train/test split). Внутренний фильтр пропускается, чтобы
        повторное удаление редких юзеров/книг после split не каскадно
        обнуляло датасет.
        """
        fit_started = time.perf_counter()
        logger.info("[IB-CF] fit: вход — %d оценок (prefiltered=%s)",
                    len(ratings_df), prefiltered)

        if prefiltered:
            df = ratings_df.copy()
            logger.info("[IB-CF] >>> filter SKIPPED (данные уже отфильтрованы)")
        else:
            with _step("filter"):
                df = filter_ratings(ratings_df, self.min_user_ratings, self.min_book_ratings)
        if df.empty:
            raise ValueError("Нет оценок для обучения — проверьте входные данные")

        with _step("build sparse matrix"):
            users_cat = pd.Categorical(df["user_id"])
            books_cat = pd.Categorical(df["book_id"])
            n_users = len(users_cat.categories)
            n_books = len(books_cat.categories)

            if n_books > _MAX_ITEMS_DENSE:
                raise ValueError(
                    f"После фильтрации {n_books} книг — item×item матрица "
                    f"~{n_books * n_books * 8 / 1e9:.1f} GB. "
                    f"Увеличьте min_book_ratings."
                )

            logger.info(
                "[IB-CF] размеры: users=%d, books=%d, ratings=%d, "
                "item-item ≈ %.2f GB (до threshold)",
                n_users, n_books, len(df), n_books * n_books * 8 / 1e9,
            )
            raw = csr_matrix(
                (df["rating"].astype(float).values, (users_cat.codes, books_cat.codes)),
                shape=(n_users, n_books),
            )

        with _step("user means + adjusted centering"):
            # User mean по оценённым items.
            sums = np.asarray(raw.sum(axis=1)).ravel()
            counts = np.asarray((raw != 0).sum(axis=1)).ravel()
            user_means = np.divide(sums, counts, out=np.zeros_like(sums), where=counts > 0)

            # Adjusted cosine: вычитаем mean ПОЛЬЗОВАТЕЛЯ (а не item-а).
            # Это правильнее именно для item-based — корректирует шкалу юзеров,
            # после чего items сравниваются "честно".
            centered = raw.copy()
            per_nnz_means = np.repeat(user_means, np.diff(raw.indptr))
            centered.data = centered.data - per_nnz_means

        with _step(f"cosine_similarity item-item {n_books}x{n_books}"):
            # cosine_similarity ждёт samples в строках. items сейчас в столбцах,
            # поэтому транспонируем — получаем матрицу (n_items, n_users).
            similarity = cosine_similarity(centered.T, dense_output=True)
            np.fill_diagonal(similarity, 0.0)  # item не похож сам на себя

        with _step(f"top-{self.k} threshold per item"):
            # Векторно для каждой строки оставляем top-k по модулю похожести.
            # Без этого матрица плотная и в формулах участвуют тысячи слабых
            # связей, что и медленно, и шумно.
            k_eff = min(self.k, n_books - 1)
            abs_sim = np.abs(similarity)
            # argpartition вдоль axis=1 — для каждой строки индексы top-k.
            top_idx = np.argpartition(abs_sim, -k_eff, axis=1)[:, -k_eff:]
            # Маска по этим индексам — True там, где элемент в top-k.
            mask = np.zeros_like(similarity, dtype=bool)
            rows = np.arange(n_books)[:, None]
            mask[rows, top_idx] = True
            similarity *= mask

            # Сжимаем в sparse — кратно ускоряет prediction (matmul по nnz).
            sim_sparse = csr_matrix(similarity)
            abs_sim_sparse = sim_sparse.copy()
            abs_sim_sparse.data = np.abs(abs_sim_sparse.data)
            logger.info(
                "[IB-CF] similarity nnz=%d (~%d per item, плотность %.3f%%)",
                sim_sparse.nnz, sim_sparse.nnz // n_books,
                100.0 * sim_sparse.nnz / (n_books * n_books),
            )

        with _step("save artifacts"):
            self._raw = raw
            self._centered = centered  # нужен для prediction с mean-add
            self._user_means = user_means
            self._similarity = sim_sparse  # хранится для статистики/диагностики
            # ВАЖНО: транспонируем здесь один раз. similarity[i, :] хранит top-k
            # соседей item i. Чтобы в predict формула pred[i] = Σ_j sim(i,j)·...
            # сводилась к row-vector matmul (u @ M), нужно умножать на similarity.T.
            # Конвертируем .tocsr() — иначе остался бы CSC и matmul был бы медленнее.
            self._similarity_T = sim_sparse.T.tocsr()
            self._abs_similarity_T = abs_sim_sparse.T.tocsr()
            self._index = _Index(
                user_ids=list(users_cat.categories),
                book_ids=list(books_cat.categories),
                user_to_idx={uid: i for i, uid in enumerate(users_cat.categories)},
                book_to_idx={bid: i for i, bid in enumerate(books_cat.categories)},
            )

        logger.info("[IB-CF] fit: ВСЁ ГОТОВО за %.2f c", time.perf_counter() - fit_started)

    # -------------------------------------------------------------- predict
    def predict_all_for_user(self, user_idx: int) -> np.ndarray:
        """Векторное предсказание оценок пользователю для ВСЕХ книг.

        Корректная формула с adjusted cosine (даёт знаковые sim):
          pred(u, i) = mean(u) + Σ_j sim(i, j) * (r(u, j) - mean(u)) / Σ_j |sim(i, j)|
        где j пробегает по items, оценённым пользователем u И попавшим в
        top-k наиболее похожих на i.

        Зачем mean-centering + add mean back:
          adjusted cosine даёт отрицательные similarities (анти-коррелированные
          items). Если умножать СЫРЫЕ оценки (4-5) на отрицательные sim, числитель
          систематически уезжает в минус, prediction становится отрицательным
          числом → RMSE улетает в небеса. Mean-centering снимает baseline-bias
          и работает с отклонениями, после чего mean возвращается обратно.

        Зачем .T у similarity:
          top-k threshold был сделан построчно: similarity[i, :] = top-k соседей i.
          Чтобы row-vector matmul (u @ M) дал Σ_j sim(i,j)·u[j] для каждого i,
          умножать нужно на similarity.T (тогда столбец i у .T = строка i у sim).
        """
        u_centered = self._centered[user_idx]                # sparse (1, n_items), mean-centered
        u_mask = self._raw[user_idx].copy()
        u_mask.data = np.ones_like(u_mask.data)              # 1 там, где пользователь оценил

        # Числитель: для каждого item i суммируем sim(i, j) * centered(u, j).
        numerator = np.asarray((u_centered @ self._similarity_T).todense()).ravel()
        # Знаменатель: суммируем |sim(i, j)| только по j, которые пользователь оценил.
        denominator = np.asarray((u_mask @ self._abs_similarity_T).todense()).ravel()

        # Items без оценённых пользователем соседей → fallback к user mean.
        no_neighbors = denominator < 1e-9
        denominator[no_neighbors] = 1.0
        predictions = self._user_means[user_idx] + numerator / denominator
        predictions[no_neighbors] = self._user_means[user_idx]
        return predictions

    def predict(self, user_id, book_id) -> float | None:
        """Точечное предсказание оценки. None — если пары нет в обучении."""
        if not self.is_fitted:
            return None
        u = self._index.user_to_idx.get(user_id)
        b = self._index.book_to_idx.get(book_id)
        if u is None or b is None:
            return None
        return float(self.predict_all_for_user(u)[b])

    def recommend(self, user_id, n: int = 10) -> list[dict]:
        """Топ-n книг, ещё не оценённых пользователем, отсортированных по предсказанию."""
        if not self.is_fitted:
            return []
        u = self._index.user_to_idx.get(user_id)
        if u is None:
            return []

        scores = self.predict_all_for_user(u)
        already = self._raw[u].indices
        scores[already] = -np.inf

        top = np.argpartition(scores, -n)[-n:]
        top = top[np.argsort(scores[top])[::-1]]
        return [
            {"book_id": self._index.book_ids[i], "predicted_rating": float(scores[i])}
            for i in top if np.isfinite(scores[i])
        ]
