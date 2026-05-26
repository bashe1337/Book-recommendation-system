"""Content-based модель рекомендаций на основе TF-IDF и косинусного сходства."""

import logging

import numpy as np
import pandas as pd
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.metrics.pairwise import cosine_similarity

logger = logging.getLogger(__name__)


class ContentBasedModel:
    """Рекомендатель на основе содержимого книг (TF-IDF профиль + косинусное сходство)."""

    def __init__(self):
        self._vectorizer: TfidfVectorizer | None = None
        # Разреженная TF-IDF матрица книг (n_books x n_features).
        self._tfidf_matrix = None
        # Плотная матрица книга-книга косинусного сходства (n_books x n_books).
        self._similarity = None
        # Отображение book_id (str) -> позиция строки в матрицах.
        self._id_to_index: dict[str, int] = {}
        # Обратное отображение позиция -> book_id для сборки результата.
        self._index_to_id: list[str] = []

    @property
    def book_count(self) -> int:
        """Количество книг, загруженных в модель."""
        return len(self._index_to_id)

    @property
    def is_fitted(self) -> bool:
        """True, если модель обучена и готова отдавать рекомендации."""
        return self._similarity is not None

    def fit(self, books_df: pd.DataFrame) -> None:
        """Векторизует тексты книг и считает матрицу попарного сходства.

        Параметры TF-IDF:
          - max_features=10000  — ограничиваем словарь самыми частыми термами,
          - ngram_range=(1, 2)  — учитываем униграммы и биграммы,
          - analyzer='word'     — токенизация по словам,
          - min_df=2            — отбрасываем термы, встречающиеся лишь в одной книге.
        """
        if books_df.empty:
            logger.warning("Обучение на пустом наборе книг — модель останется пустой")
            self._reset()
            return

        # Восстанавливаем числовые индексы 0..n-1, чтобы позиция строки совпадала
        # с позицией в TF-IDF матрице.
        df = books_df.reset_index(drop=True)
        self._index_to_id = [str(bid) for bid in df["book_id"].tolist()]
        self._id_to_index = {bid: i for i, bid in enumerate(self._index_to_id)}

        logger.info("Обучение TF-IDF на %d книгах...", len(df))
        self._vectorizer = TfidfVectorizer(
            max_features=10000,
            ngram_range=(1, 2),
            analyzer="word",
            min_df=2,
        )
        self._tfidf_matrix = self._vectorizer.fit_transform(df["content"].fillna(""))

        # Косинусное сходство между всеми парами книг. linear_kernel дал бы тот же
        # результат на L2-нормированных TF-IDF векторах, но cosine_similarity нагляднее.
        self._similarity = cosine_similarity(self._tfidf_matrix)
        logger.info(
            "Модель обучена: %d книг, %d признаков",
            self._tfidf_matrix.shape[0],
            self._tfidf_matrix.shape[1],
        )

    def get_similar_books(self, book_id: str, n: int = 10) -> list[dict]:
        """Возвращает топ-n книг, похожих на заданную (саму книгу исключаем).

        Если книги нет в матрице — возвращает пустой список (вызывающий код
        отдаёт 404).
        """
        idx = self._id_to_index.get(str(book_id))
        if idx is None:
            logger.info("Книга %s не найдена в модели", book_id)
            return []

        scores = self._similarity[idx]
        # argsort по убыванию; первый элемент — сама книга (сходство = 1.0).
        order = np.argsort(scores)[::-1]
        results = []
        for pos in order:
            if pos == idx:
                continue  # исключаем саму книгу
            results.append(
                {
                    "book_id": self._index_to_id[pos],
                    "similarity_score": float(scores[pos]),
                }
            )
            if len(results) >= n:
                break
        return results

    def get_recommendations_for_user(self, rated_books: dict, n: int = 10) -> list[dict]:
        """Строит персональные рекомендации по оценкам пользователя.

        Профиль пользователя = взвешенная сумма TF-IDF векторов оценённых книг:
          - вес = оценка (1..5),
          - книги с оценкой <= 2 вычитаются из профиля (анти-предпочтения).
        Затем считаем косинусное сходство профиля со всеми книгами и отдаём
        топ-n, исключая уже оценённые.
        """
        if not rated_books or not self.is_fitted:
            return []

        # Аккумулятор профиля в пространстве признаков TF-IDF.
        profile = np.zeros((1, self._tfidf_matrix.shape[1]))
        rated_indices: set[int] = set()
        used = 0

        for book_id, score in rated_books.items():
            idx = self._id_to_index.get(str(book_id))
            if idx is None:
                continue  # книги нет в каталоге модели — пропускаем
            rated_indices.add(idx)
            vector = self._tfidf_matrix[idx].toarray()
            # Положительный вклад для понравившихся, отрицательный — для оценок <= 2.
            weight = float(score) if score > 2 else -float(score)
            profile += weight * vector
            used += 1

        if used == 0:
            return []

        # Косинусное сходство профиля со всеми книгами.
        scores = cosine_similarity(profile, self._tfidf_matrix)[0]
        order = np.argsort(scores)[::-1]

        results = []
        for pos in order:
            if pos in rated_indices:
                continue  # не рекомендуем уже оценённое
            results.append(
                {
                    "book_id": self._index_to_id[pos],
                    "similarity_score": float(scores[pos]),
                }
            )
            if len(results) >= n:
                break
        return results

    def _reset(self) -> None:
        """Сбрасывает состояние модели в необученное."""
        self._vectorizer = None
        self._tfidf_matrix = None
        self._similarity = None
        self._id_to_index = {}
        self._index_to_id = []
