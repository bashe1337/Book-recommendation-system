"""SVD (FunkSVD с biases) — собственная numpy-реализация для explicit feedback.

Алгоритм (Simon Funk, 2006, Netflix Prize):
  r̂(u, i) = μ + b_u + b_i + p_u · q_iᵀ
    μ   — глобальное среднее всех оценок (нормализует абсолютный уровень)
    b_u — bias пользователя (учитывает оптимистов и строгих критиков)
    b_i — bias книги (популярная среди всех / непопулярная)
    p_u, q_i — латентные факторы размерности k (вкусы пользователя и "темы" книги)

Целевая функция:
  Σ_{(u,i) ∈ R} (r_ui − r̂_ui)²  +  λ · (||p_u||² + ||q_i||² + b_u² + b_i²)

Минимизация — стохастический градиентный спуск (SGD): берём по одному примеру
(u, i, r), считаем ошибку err = r − r̂(u,i), шагаем в направлении уменьшения
ошибки с L2-регуляризацией:

  b_u ← b_u + lr · (err − λ · b_u)
  b_i ← b_i + lr · (err − λ · b_i)
  p_u ← p_u + lr · (err · q_i − λ · p_u)
  q_i ← q_i + lr · (err · p_u_old − λ · q_i)

Чем SVD сильнее ALS implicit на explicit данных:
  • Loss считается ТОЛЬКО по наблюдаемым (u, i) парам — пустая ячейка значит
    "не оценил", а не "не понравилось".
  • Bias-члены отделяют "оптимистов" и "любимых толпой" — латентные факторы
    остаются "чистыми" предпочтениями.
  • Прогноз — число в шкале оценок (1..5), напрямую сравнимое с фактом → RMSE/MAE.
  • Естественно работает со знаковыми отклонениями: bias −0.5 + латент −1.0
    = прогноз μ − 1.5 (низкая оценка).

Реализация на чистом numpy — без Cython/build-tools, работает на любом
Python. На BX-выборке (~10K ratings после фильтра) обучается за 3–10 секунд.
"""

import logging
import time
from contextlib import contextmanager
from dataclasses import dataclass

import numpy as np
import pandas as pd
from scipy.sparse import csr_matrix

from models.collaborative import filter_ratings

logger = logging.getLogger(__name__)


@contextmanager
def _step(name: str):
    """Контекстный менеджер для тайминга этапа."""
    logger.info("[SVD] >>> %s ...", name)
    t0 = time.perf_counter()
    yield
    logger.info("[SVD] <<< %s — %.2f c", name, time.perf_counter() - t0)


@dataclass
class _Index:
    """Соответствие исходных id и позиций в матрицах факторов."""
    user_ids: list
    book_ids: list
    user_to_idx: dict
    book_to_idx: dict


class SVDModel:
    """FunkSVD на собственной numpy-реализации. Тот же интерфейс что у CF-моделей."""

    def __init__(
        self,
        n_factors: int = 50,
        n_epochs: int = 20,
        lr: float = 0.005,
        reg: float = 0.02,
        min_user_ratings: int = 5,
        min_book_ratings: int = 10,
        rating_scale: tuple[float, float] = (1.0, 5.0),
        random_state: int = 42,
        init_std: float = 0.1,
    ):
        self.n_factors = n_factors      # размерность латентного пространства
        self.n_epochs = n_epochs        # сколько проходов SGD по всему датасету
        self.lr = lr                    # learning rate, общий для всех параметров
        self.reg = reg                  # L2-регуляризация, общая
        # Фильтры активности (см. filter_ratings).
        self.min_user_ratings = min_user_ratings
        self.min_book_ratings = min_book_ratings
        # rating_scale — допустимый диапазон. Сохраняется для статистики; модель
        # сама прогнозы НЕ клипует (предсказания могут чуть выходить из шкалы —
        # это нормально, клиппинг при желании делается на стороне consumer).
        self.rating_scale = rating_scale
        self.random_state = random_state
        # std инициализации факторов. Слишком большое → "взрыв" начальной ошибки,
        # слишком малое (~0) → парализует gradient flow. 0.1 — стандарт.
        self.init_std = init_std

        # Параметры модели. Заполняются в fit().
        self._P: np.ndarray | None = None       # (n_users, n_factors)
        self._Q: np.ndarray | None = None       # (n_items, n_factors)
        self._bu: np.ndarray | None = None      # (n_users,) bias пользователя
        self._bi: np.ndarray | None = None      # (n_items,) bias книги
        self._global_mean: float = 0.0
        self._raw: csr_matrix | None = None     # для маски "уже оценено"
        self._index: _Index | None = None
        # История обучения — для диагностики переобучения через UI / логи.
        self._train_rmse_history: list[float] = []

    @property
    def is_fitted(self) -> bool:
        return self._P is not None

    @property
    def stats(self) -> dict:
        if not self.is_fitted:
            return {"fitted": False}
        return {
            "fitted": True,
            "users": len(self._index.user_ids),
            "books": len(self._index.book_ids),
            "ratings": int(self._raw.nnz),
            "n_factors": self.n_factors,
            "n_epochs": self.n_epochs,
            "lr": self.lr,
            "reg": self.reg,
            "global_mean": float(self._global_mean),
            "rating_scale": list(self.rating_scale),
            "final_train_rmse": (self._train_rmse_history[-1]
                                 if self._train_rmse_history else None),
        }

    # ------------------------------------------------------------------ fit
    def fit(self, ratings_df: pd.DataFrame, prefiltered: bool = False) -> None:
        """Обучение SVD стохастическим градиентным спуском.

        prefiltered=True — данные уже отфильтрованы снаружи (правильный pipeline
        filter → per_user_split → fit). Внутренний фильтр пропускается.
        """
        fit_started = time.perf_counter()
        logger.info("[SVD] fit: вход — %d оценок (prefiltered=%s, scale=%s)",
                    len(ratings_df), prefiltered, self.rating_scale)

        if prefiltered:
            df = ratings_df.copy()
            logger.info("[SVD] >>> filter SKIPPED (данные уже отфильтрованы)")
        else:
            with _step("filter"):
                df = filter_ratings(ratings_df, self.min_user_ratings, self.min_book_ratings)
        if df.empty:
            raise ValueError("Нет оценок для обучения")

        with _step("build index + train arrays"):
            # Categorical коды дают компактные 0..n индексы для P/Q матриц.
            users_cat = pd.Categorical(df["user_id"])
            books_cat = pd.Categorical(df["book_id"])
            n_users = len(users_cat.categories)
            n_books = len(books_cat.categories)

            # Sparse матрица сырых оценок — нужна для маски "уже оценено" в recommend().
            self._raw = csr_matrix(
                (df["rating"].astype(float).values,
                 (users_cat.codes, books_cat.codes)),
                shape=(n_users, n_books),
            )
            self._index = _Index(
                user_ids=list(users_cat.categories),
                book_ids=list(books_cat.categories),
                user_to_idx={uid: i for i, uid in enumerate(users_cat.categories)},
                book_to_idx={bid: i for i, bid in enumerate(books_cat.categories)},
            )

            # Плоские numpy-массивы для горячего SGD-цикла.
            train_users = users_cat.codes.astype(np.int64)
            train_items = books_cat.codes.astype(np.int64)
            train_ratings = df["rating"].astype(np.float64).values
            n_ratings = len(train_ratings)
            logger.info("[SVD] %d юзеров × %d книг × %d оценок, n_factors=%d",
                        n_users, n_books, n_ratings, self.n_factors)

        with _step("init parameters"):
            rng = np.random.default_rng(self.random_state)
            # P, Q инициализируем нормальным шумом — даёт начальное разнообразие
            # факторов. Biases начинаем с 0 — они быстро подстроятся под средние
            # отклонения от μ.
            self._P = rng.normal(0.0, self.init_std, size=(n_users, self.n_factors))
            self._Q = rng.normal(0.0, self.init_std, size=(n_books, self.n_factors))
            self._bu = np.zeros(n_users)
            self._bi = np.zeros(n_books)
            self._global_mean = float(train_ratings.mean())
            logger.info("[SVD] global_mean = %.4f", self._global_mean)

        # ----------------------------- SGD ---------------------------------
        # Локальные алиасы — точечный доступ self._X в горячем цикле дороже
        # из-за лишнего lookup на каждом обращении. Это даёт ~30% ускорения.
        P, Q, bu, bi = self._P, self._Q, self._bu, self._bi
        mu = self._global_mean
        lr, reg = self.lr, self.reg

        epochs_start = time.perf_counter()
        for epoch in range(1, self.n_epochs + 1):
            # Перетасовываем порядок примеров каждую эпоху — критично для SGD,
            # иначе модель учит "путь по данным", а не сами данные.
            order = rng.permutation(n_ratings)
            sum_sq_err = 0.0

            for idx in order:
                u = train_users[idx]
                i = train_items[idx]
                r = train_ratings[idx]

                # Прогноз и ошибка по текущим параметрам
                dot = P[u] @ Q[i]
                pred = mu + bu[u] + bi[i] + dot
                err = r - pred
                sum_sq_err += err * err

                # Обновление biases (всё скалярно)
                bu[u] += lr * (err - reg * bu[u])
                bi[i] += lr * (err - reg * bi[i])

                # Обновление факторов. ВАЖНО: при обновлении Q[i] нужен СТАРЫЙ
                # P[u] (до его собственного шага), иначе это уже другой алгоритм.
                # Классический Funk обновляет так.
                p_u_old = P[u].copy()
                P[u] += lr * (err * Q[i] - reg * P[u])
                Q[i] += lr * (err * p_u_old - reg * Q[i])

            rmse = float(np.sqrt(sum_sq_err / n_ratings))
            self._train_rmse_history.append(rmse)

            # Логируем 1-ю, последнюю и каждую 5-ю эпоху, чтобы не спамить.
            if epoch == 1 or epoch == self.n_epochs or epoch % 5 == 0:
                avg_epoch = (time.perf_counter() - epochs_start) / epoch
                eta = (self.n_epochs - epoch) * avg_epoch
                logger.info("[SVD] epoch %d/%d: train RMSE=%.4f "
                            "(%.2fс/эпоха, ETA %.0fс)",
                            epoch, self.n_epochs, rmse, avg_epoch, eta)

        # Алиасы мутируют те же массивы, что и self._X — финальное присваивание
        # для ясности (необязательное, но улучшает читаемость).
        self._P, self._Q, self._bu, self._bi = P, Q, bu, bi
        logger.info("[SVD] fit: ВСЁ ГОТОВО за %.2f c", time.perf_counter() - fit_started)

    # -------------------------------------------------------------- predict
    def predict_all_for_user(self, user_idx: int) -> np.ndarray:
        """Векторное предсказание оценок пользователю для ВСЕХ книг.

        r̂(u, i) = μ + b_u + b_i + p_u · q_i — один matmul на ~миллисекунду.
        """
        latent = self._Q @ self._P[user_idx]   # (n_books,) — латентная составляющая
        return self._global_mean + self._bu[user_idx] + self._bi + latent

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
        """Топ-n книг, ещё не оценённых пользователем."""
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
