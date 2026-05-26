"""Загрузка данных из PostgreSQL для построения content-based модели.

Подключается к той же БД (brs_db), что и .NET-бэкенд. Имена колонок в схеме —
PascalCase, поэтому в SQL они обёрнуты в двойные кавычки ("Id", "Title", ...).
"""

import logging
import os

import pandas as pd
import psycopg2
from dotenv import load_dotenv

load_dotenv()

logger = logging.getLogger(__name__)

# Строка подключения берётся из .env (DATABASE_URL). psycopg2 понимает URI-формат.
DATABASE_URL = os.getenv("DATABASE_URL")


def _connect():
    """Открывает новое соединение с БД. Бросает исключение, если DATABASE_URL не задан."""
    if not DATABASE_URL:
        raise RuntimeError("DATABASE_URL не задан — проверьте файл .env")
    return psycopg2.connect(DATABASE_URL)


# Один запрос с агрегированными подзапросами: для каждой книги собираем
# имена авторов / жанров / тегов в строки через string_agg. Это избавляет
# от размножения строк книги при JOIN и от группировки на стороне Python.
_BOOKS_QUERY = """
SELECT
    b."Id"                                    AS book_id,
    b."Title"                                 AS title,
    COALESCE(b."Description", '')             AS description,
    COALESCE(b."Language", '')                AS language,
    COALESCE(b."AvgRating", 0)                AS avg_rating,
    COALESCE(a.names, '')                     AS authors,
    COALESCE(g.names, '')                     AS genres,
    COALESCE(t.names, '')                     AS tags
FROM books b
LEFT JOIN (
    SELECT ba."BookId" AS book_id, string_agg(au."Name", ' ') AS names
    FROM book_authors ba
    JOIN authors au ON au."Id" = ba."AuthorId"
    GROUP BY ba."BookId"
) a ON a.book_id = b."Id"
LEFT JOIN (
    SELECT bg."BookId" AS book_id, string_agg(ge."Name", ' ') AS names
    FROM book_genres bg
    JOIN genres ge ON ge."Id" = bg."GenreId"
    GROUP BY bg."BookId"
) g ON g.book_id = b."Id"
LEFT JOIN (
    SELECT bt."BookId" AS book_id, string_agg(tg."Name", ' ') AS names
    FROM book_tags bt
    JOIN tags tg ON tg."Id" = bt."TagId"
    GROUP BY bt."BookId"
) t ON t.book_id = b."Id";
"""


def _build_content(row: pd.Series) -> str:
    """Формирует текстовое поле "content" с весами через повторение токенов.

    Веса заданы не числами, а кратностью повторения — TF-IDF сам учтёт
    возросшую частоту терма:
      - название книги  — x3 (самый сильный сигнал схожести),
      - имена авторов   — x2,
      - названия жанров — x2,
      - названия тегов  — x1,
      - описание книги  — x1.
    """
    title = str(row["title"] or "")
    authors = str(row["authors"] or "")
    genres = str(row["genres"] or "")
    tags = str(row["tags"] or "")
    description = str(row["description"] or "")

    parts = [
        title, title, title,        # вес x3
        authors, authors,           # вес x2
        genres, genres,             # вес x2
        tags,                       # вес x1
        description,                # вес x1
    ]
    # Склеиваем через пробел и нормализуем регистр — TF-IDF чувствителен к нему,
    # а нижний регистр повышает совпадаемость термов между книгами.
    return " ".join(p for p in parts if p).lower()


def load_books() -> pd.DataFrame:
    """Загружает все книги с авторами, жанрами и тегами и возвращает DataFrame.

    В результирующем DataFrame есть колонка "content" — объединённый
    взвешенный текст для TF-IDF векторизации.
    """
    logger.info("Загрузка книг из БД...")
    with _connect() as conn:
        df = pd.read_sql(_BOOKS_QUERY, conn)

    if df.empty:
        logger.warning("Из БД не загружено ни одной книги")
        df["content"] = pd.Series(dtype="object")
        return df

    df["content"] = df.apply(_build_content, axis=1)
    logger.info("Загружено книг: %d", len(df))
    return df


def load_user_ratings(user_id: str) -> dict:
    """Загружает оценки конкретного пользователя.

    Возвращает словарь { book_id (str): score (int) }. Пустой словарь,
    если пользователь ничего не оценивал.
    """
    query = 'SELECT "BookId", "Score" FROM ratings WHERE "UserId" = %s;'
    with _connect() as conn:
        with conn.cursor() as cur:
            cur.execute(query, (user_id,))
            rows = cur.fetchall()

    # Ключи приводим к str, чтобы единообразно сопоставлять с book_id из DataFrame.
    ratings = {str(book_id): int(score) for book_id, score in rows}
    logger.info("Пользователь %s: загружено оценок %d", user_id, len(ratings))
    return ratings


def load_all_ratings() -> pd.DataFrame:
    """Все оценки из BRS — для обучения CF на реальных пользователях.

    Шкала Score: 1..5 (см. check-constraint ck_ratings_score_range).
    user_id и book_id приводим к строкам, чтобы единообразно работать с
    Categorical-индексами в модели.
    """
    query = (
        'SELECT "UserId"::text AS user_id, '
        '"BookId"::text AS book_id, '
        '"Score" AS rating '
        'FROM ratings;'
    )
    with _connect() as conn:
        df = pd.read_sql(query, conn)
    df["rating"] = df["rating"].astype(int)
    logger.info("BRS ratings: загружено %d оценок", len(df))
    return df


def load_all_interactions(
    weight_view: float = 1.0,
    weight_favorite: float = 4.0,
    weight_rating_multiplier: float = 1.0,
) -> pd.DataFrame:
    """Объединённые сигналы интереса из BRS для ALS (Hu et al., 2008).

    Складываем три источника в единую матрицу confidence:
      • views (view_history)  — слабый сигнал (вес 1 за каждый просмотр)
      • favorites             — сильный сигнал (вес 4 за добавление в избранное)
      • ratings (1..5)        — взвешенный явный сигнал (score × multiplier)

    Веса параметризованы, чтобы их можно было пересчитать без правки SQL.
    Возвращает DataFrame с колонками (user_id, book_id, confidence).
    """
    # Объединяем три таблицы через UNION ALL, потом аггрегируем по паре (user, book).
    # Числовые литералы для весов подставляем напрямую — psycopg не любит когда float
    # ходит как parameter в UNION ALL, проще inline.
    query = f"""
        SELECT user_id, book_id, SUM(weight) AS confidence
        FROM (
            SELECT "UserId"::text AS user_id, "BookId"::text AS book_id,
                   {weight_view}::float AS weight
            FROM view_history
            UNION ALL
            SELECT "UserId"::text, "BookId"::text,
                   {weight_favorite}::float
            FROM favorites
            UNION ALL
            SELECT "UserId"::text, "BookId"::text,
                   ("Score" * {weight_rating_multiplier})::float
            FROM ratings
        ) all_signals
        GROUP BY user_id, book_id;
    """
    with _connect() as conn:
        df = pd.read_sql(query, conn)
    df["confidence"] = df["confidence"].astype(float)
    logger.info(
        "BRS interactions: %d пар (user, book) с агрегированным confidence "
        "(weights: view=%.1f, fav=%.1f, rating×%.1f)",
        len(df), weight_view, weight_favorite, weight_rating_multiplier,
    )
    return df
