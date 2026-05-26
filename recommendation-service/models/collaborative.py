"""User-based Collaborative Filtering на mean-centered cosine similarity.

Алгоритм:
  1) Строим разреженную матрицу user × item с оценками.
  2) Вычитаем среднюю оценку каждого пользователя — снимаем "оптимизм" и "строгость".
  3) Считаем user-user косинусное сходство по mean-centered матрице.
  4) Прогноз = средняя оценка пользователя + взвешенное отклонение оценок k
     наиболее похожих соседей, оценивших книгу.
"""

import logging
import time
from contextlib import contextmanager
from dataclasses import dataclass

import numpy as np
import pandas as pd
from scipy.sparse import csr_matrix
from sklearn.metrics.pairwise import cosine_similarity

logger = logging.getLogger(__name__)

# Жёсткий предел на количество пользователей в плотной матрице сходства:
# матрица занимает n_users^2 * 8 байт, при 20k это уже ~3.2 GB.
_MAX_USERS_DENSE = 20000


@contextmanager
def _step(name: str):
    """Контекстный менеджер для тайминга этапа: логирует начало и длительность."""
    logger.info("[CF] >>> %s ...", name)
    t0 = time.perf_counter()
    yield
    logger.info("[CF] <<< %s — %.2f c", name, time.perf_counter() - t0)


def filter_ratings(
    df: pd.DataFrame,
    min_user_ratings: int,
    min_book_ratings: int,
    iterations: int = 3,
) -> pd.DataFrame:
    """Итеративно отбрасывает редких пользователей и книги.

    Несколько проходов нужны потому, что удаление книг может уронить число
    оценок у пользователей ниже порога — и наоборот. Обычно сходится за 2-3 итерации.

    Вынесено на уровень модуля, чтобы можно было применить ДО train/test split:
    тогда обе подвыборки гарантированно содержат только пользователей и книги,
    которые останутся в модели после обучения — иначе Precision/Recall на тесте
    оценивает несуществующие в модели элементы и метрики деградируют до нуля.
    """
    before = len(df)
    for _ in range(iterations):
        bc = df["book_id"].value_counts()
        df = df[df["book_id"].isin(bc[bc >= min_book_ratings].index)]
        uc = df["user_id"].value_counts()
        df = df[df["user_id"].isin(uc[uc >= min_user_ratings].index)]
    logger.info(
        "Фильтрация: %d -> %d оценок (min_user=%d, min_book=%d)",
        before, len(df), min_user_ratings, min_book_ratings,
    )
    return df.copy()


def per_user_train_test_split(
    df: pd.DataFrame,
    test_size: float = 0.2,
    random_state: int = 42,
) -> tuple[pd.DataFrame, pd.DataFrame]:
    """Holdout-разбиение **по каждому пользователю**: каждый юзер представлен
    и в train, и в test (если у него ≥2 оценок).

    Зачем не sklearn train_test_split: тот делит случайно по всему датасету,
    из-за чего часть пользователей может полностью попасть в test → модель
    их не видит при обучении → метрики Precision@K для них принципиально
    невычислимы. Per-user split эту дыру закрывает.
    """
    rng = np.random.default_rng(random_state)
    test_indices: list[np.ndarray] = []

    # groupby(...).indices возвращает dict[user_id, np.ndarray позиций] — быстро.
    for _, positions in df.groupby("user_id", sort=False).indices.items():
        n = len(positions)
        if n < 2:
            continue  # одиночные оценки оставляем целиком в train
        n_test = max(1, int(round(n * test_size)))
        n_test = min(n_test, n - 1)  # минимум 1 оценка должна остаться в train
        test_indices.append(rng.choice(positions, size=n_test, replace=False))

    test_pos = np.concatenate(test_indices) if test_indices else np.array([], dtype=int)
    test_df = df.iloc[test_pos]
    train_df = df.drop(df.index[test_pos])
    logger.info(
        "Per-user split: train=%d, test=%d (≥1 в train у каждого юзера)",
        len(train_df), len(test_df),
    )
    return train_df, test_df


@dataclass
class _Index:
    """Соответствие исходных id и позиций в матрице."""
    user_ids: list
    book_ids: list
    user_to_idx: dict
    book_to_idx: dict


class UserBasedCFModel:
    """User-based CF с k ближайших соседей."""

    def __init__(
        self,
        k_neighbors: int = 30,
        min_user_ratings: int = 10,
        min_book_ratings: int = 20,
    ):
        # k — сколько соседей берём в расчёт прогноза. Маленькое k => шумно,
        # большое k => прогноз скатывается к среднему по корпусу.
        self.k = k_neighbors
        # Минимумы по активности — без фильтрации матрица слишком разреженная,
        # а сходство пользователей с 1-2 общими книгами шумное.
        self.min_user_ratings = min_user_ratings
        self.min_book_ratings = min_book_ratings

        self._raw: csr_matrix | None = None         # сырые оценки (нужна маска "оценено")
        self._centered: csr_matrix | None = None    # mean-centered версия
        self._user_means: np.ndarray | None = None
        self._similarity: np.ndarray | None = None  # dense (n_users x n_users)
        self._index: _Index | None = None

    @property
    def is_fitted(self) -> bool:
        return self._similarity is not None

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
        }

    # ------------------------------------------------------------------ fit
    def fit(self, ratings_df: pd.DataFrame, prefiltered: bool = False) -> None:
        """Обучение: фильтрация → построение матрицы → user-user similarity.

        Параметры ratings_df: колонки user_id, book_id, rating.

        prefiltered=True пропускает внутреннюю фильтрацию. Используется когда
        данные уже отфильтрованы ДО train/test split (иначе split откусит
        часть оценок и юзеры перестанут проходить min_user_ratings — каскадно
        обнулится весь датасет).
        """
        fit_started = time.perf_counter()
        logger.info("[CF] fit: вход — %d оценок (prefiltered=%s)", len(ratings_df), prefiltered)

        if prefiltered:
            df = ratings_df.copy()
            logger.info("[CF] >>> filter SKIPPED (данные уже отфильтрованы)")
        else:
            with _step("filter"):
                df = self._filter(ratings_df)
        if df.empty:
            raise ValueError(
                "Нет оценок для обучения — проверьте входные данные и пороги фильтрации"
            )

        with _step("build sparse matrix"):
            # Категориальные коды дают компактные 0..n индексы для sparse-матрицы.
            users_cat = pd.Categorical(df["user_id"])
            books_cat = pd.Categorical(df["book_id"])
            n_users = len(users_cat.categories)
            n_books = len(books_cat.categories)

            if n_users > _MAX_USERS_DENSE:
                raise ValueError(
                    f"После фильтрации {n_users} пользователей — это даст матрицу "
                    f"сходства ~{n_users * n_users * 8 / 1e9:.1f} GB. "
                    f"Увеличьте min_user_ratings/min_book_ratings."
                )

            logger.info(
                "[CF] размеры после фильтрации: users=%d, books=%d, ratings=%d, "
                "similarity matrix ≈ %.2f GB",
                n_users, n_books, len(df), n_users * n_users * 8 / 1e9,
            )
            raw = csr_matrix(
                (df["rating"].astype(float).values, (users_cat.codes, books_cat.codes)),
                shape=(n_users, n_books),
            )

        with _step("compute user means"):
            sums = np.asarray(raw.sum(axis=1)).ravel()
            counts = np.asarray((raw != 0).sum(axis=1)).ravel()
            user_means = np.divide(sums, counts, out=np.zeros_like(sums), where=counts > 0)

        with _step("mean-centering"):
            # Векторизованное mean-centering: вычитаем mean только из ненулевых элементов.
            centered = raw.copy()
            per_nnz_means = np.repeat(user_means, np.diff(raw.indptr))
            centered.data = centered.data - per_nnz_means

        with _step(f"cosine_similarity {n_users}x{n_users} (САМЫЙ ДОРОГОЙ ЭТАП)"):
            similarity = cosine_similarity(centered, dense_output=True)
            np.fill_diagonal(similarity, 0.0)

        with _step("save artifacts"):
            self._raw = raw
            self._centered = centered
            self._user_means = user_means
            self._similarity = similarity
            self._index = _Index(
                user_ids=list(users_cat.categories),
                book_ids=list(books_cat.categories),
                user_to_idx={uid: i for i, uid in enumerate(users_cat.categories)},
                book_to_idx={bid: i for i, bid in enumerate(books_cat.categories)},
            )

        logger.info("[CF] fit: ВСЁ ГОТОВО за %.2f c", time.perf_counter() - fit_started)

    def _filter(self, df: pd.DataFrame) -> pd.DataFrame:
        """Делегирует фильтрацию модульной функции filter_ratings.

        Оставлен для совместимости: если fit() вызвать напрямую (без
        предварительной фильтрации), он всё равно сработает корректно.
        При правильном workflow (filter → split → fit) данные к этому
        моменту уже отфильтрованы и проход idempotent.
        """
        return filter_ratings(df, self.min_user_ratings, self.min_book_ratings)

    # -------------------------------------------------------------- predict
    def predict_all_for_user(self, user_idx: int) -> np.ndarray:
        """Векторное предсказание оценок пользователю user_idx для всех книг.

        Формула: pred(u, i) = mean_u + Σ_v sim(u,v) * (r(v,i) - mean_v) / Σ_v |sim(u,v)|,
        где v — k наиболее похожих пользователей с положительным sim, оценивших книгу i.
        """
        sims = self._similarity[user_idx]                       # (n_users,)
        # Берём k ближайших с положительной похожестью — отрицательная похожесть
        # в неявных данных шумна, отбрасываем.
        top = np.argpartition(sims, -self.k)[-self.k:]
        top = top[sims[top] > 0]

        if len(top) == 0:
            # Холодный пользователь — возвращаем его среднее как baseline.
            return np.full(self._centered.shape[1], self._user_means[user_idx])

        weights = sims[top]                                     # (k,)
        neighbor_centered = self._centered[top]                 # sparse (k, n_books)
        neighbor_mask = (self._raw[top] != 0).astype(float)     # sparse (k, n_books)

        numerator = weights @ neighbor_centered                 # (n_books,)
        denominator = np.abs(weights) @ neighbor_mask           # (n_books,)
        # Защита от деления на ноль для книг, не оценённых соседями.
        denominator = np.where(denominator == 0, 1.0, denominator)

        prediction = self._user_means[user_idx] + (
            np.asarray(numerator).ravel() / np.asarray(denominator).ravel()
        )
        return prediction

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
        # Исключаем то, что пользователь уже оценил — нет смысла рекомендовать.
        already = self._raw[u].indices
        scores[already] = -np.inf

        top = np.argpartition(scores, -n)[-n:]
        top = top[np.argsort(scores[top])[::-1]]
        return [
            {"book_id": self._index.book_ids[i], "predicted_rating": float(scores[i])}
            for i in top if np.isfinite(scores[i])
        ]
