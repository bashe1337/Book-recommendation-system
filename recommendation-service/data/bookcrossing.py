"""Загрузка датасета Book-Crossing.

Поддерживаются две распространённые версии:
  • классическая (BX-Book-Ratings.csv / BX-Books.csv, sep=';', latin-1)
  • somnambwl на Kaggle (Ratings.csv / Books.csv, sep=',', utf-8)

Разделитель и кодировка определяются автоматически по содержимому файла.
"""

import csv
import logging
import os
from pathlib import Path

import pandas as pd

logger = logging.getLogger(__name__)

# Папка с CSV-файлами датасета. Относительный путь резолвится от CWD процесса.
BX_PATH = os.getenv("BX_DATASET_PATH", "./data/bookcrossing")


def _find_by_keywords(must_contain: list[str], must_not_contain: list[str] = ()) -> Path:
    """Ищет CSV в папке датасета по подстрокам в имени (case-insensitive).

    Это устойчивее к разным наименованиям между версиями датасета:
      • "ratings.csv", "Ratings.csv", "BX-Book-Ratings.csv" — все находятся одинаково.
    """
    folder = Path(BX_PATH)
    if not folder.exists():
        raise FileNotFoundError(
            f"Папка {folder.resolve()} не существует. "
            f"Скачайте датасет https://www.kaggle.com/datasets/somnambwl/bookcrossing-dataset "
            f"и распакуйте CSV в неё (или задайте BX_DATASET_PATH в .env)."
        )

    matches = []
    for p in folder.iterdir():
        if not p.is_file() or p.suffix.lower() != ".csv":
            continue
        name_low = p.stem.lower()
        if all(kw in name_low for kw in must_contain) and not any(
            kw in name_low for kw in must_not_contain
        ):
            matches.append(p)

    if not matches:
        raise FileNotFoundError(
            f"В {folder.resolve()} не найден CSV содержащий {must_contain} "
            f"(и не содержащий {list(must_not_contain)}). "
            f"Имеющиеся файлы: {[p.name for p in folder.iterdir() if p.suffix == '.csv']}"
        )
    # При нескольких совпадениях берём наибольший — обычно это нужный.
    matches.sort(key=lambda p: p.stat().st_size, reverse=True)
    return matches[0]


def _sniff_dialect(path: Path) -> tuple[str, str]:
    """Определяет разделитель и кодировку файла.

    Сначала пробуем UTF-8 (somnambwl-версия), затем latin-1 (классический BX).
    Разделитель определяем через csv.Sniffer на первых ~32 KB файла.
    """
    encodings = ["utf-8", "latin-1"]
    chosen_enc = None
    sample = ""
    for enc in encodings:
        try:
            with open(path, "r", encoding=enc, errors="strict") as f:
                sample = f.read(32_000)
            chosen_enc = enc
            break
        except UnicodeDecodeError:
            continue
    if chosen_enc is None:
        chosen_enc = "latin-1"
        with open(path, "r", encoding=chosen_enc, errors="replace") as f:
            sample = f.read(32_000)

    # Sniffer хорошо определяет, но иногда падает — fallback по простому подсчёту.
    try:
        dialect = csv.Sniffer().sniff(sample, delimiters=";,\t")
        sep = dialect.delimiter
    except csv.Error:
        first_line = sample.split("\n", 1)[0]
        sep = ";" if first_line.count(";") > first_line.count(",") else ","

    logger.info("[BX] %s → encoding=%s, sep=%r, size=%.1f MB",
                path.name, chosen_enc, sep, path.stat().st_size / 1e6)
    return sep, chosen_enc


def load_ratings() -> pd.DataFrame:
    """Загружает явные оценки Book-Crossing (rating > 0).

    BX содержит явные оценки (1..10) и неявные (0 = просто прочитано/добавлено).
    Для расчёта RMSE/MAE нам нужны только явные.
    Возвращает DataFrame с колонками user_id (int), book_id (str — ISBN), rating (int).
    """
    path = _find_by_keywords(must_contain=["rating"])
    sep, enc = _sniff_dialect(path)

    logger.info("[BX] чтение ratings %s ...", path.name)
    df = pd.read_csv(path, sep=sep, encoding=enc, on_bad_lines="skip",
                     dtype=str, low_memory=False)
    df.columns = [c.strip().lower().replace("-", "_") for c in df.columns]
    # somnambwl: User-ID, ISBN, Book-Rating  → user_id, isbn, book_rating
    # классика:  User-ID;ISBN;Book-Rating    → то же самое после нормализации
    df = df.rename(columns={"book_rating": "rating", "isbn": "book_id"})

    df["rating"] = pd.to_numeric(df["rating"], errors="coerce")
    df["user_id"] = pd.to_numeric(df["user_id"], errors="coerce")
    df = df.dropna(subset=["rating", "user_id", "book_id"])
    df = df[df["rating"] > 0]                # отбрасываем неявные (0)
    df["rating"] = df["rating"].astype(int)
    df["user_id"] = df["user_id"].astype(int)

    logger.info("BX ratings: загружено %d явных оценок", len(df))
    return df[["user_id", "book_id", "rating"]].reset_index(drop=True)


def rescale_ratings(df: pd.DataFrame, target_max: int, source_max: int = 10) -> pd.DataFrame:
    """Линейный пересчёт оценок в шкалу 1..target_max.

    Используется, чтобы выровнять шкалу BX (1..10) со шкалой BRS (1..5).
    Округляем до целого и клипуем в [1, target_max] — оценка 0 после
    масштабирования невозможна, т.к. на входе уже отфильтровано rating > 0.
    """
    if target_max == source_max:
        return df
    scaled = df.copy()
    scaled["rating"] = (
        (scaled["rating"] * (target_max / source_max))
        .round()
        .clip(1, target_max)
        .astype(int)
    )
    logger.info("BX ratings: пересчитаны со шкалы 1..%d в 1..%d", source_max, target_max)
    return scaled


def load_books_meta() -> pd.DataFrame:
    """Загружает метаданные книг (ISBN -> Title, Author).

    Используется только для обогащения ответа рекомендаций; модель CF к
    тексту не обращается.
    """
    # "books" но не "ratings" — иначе Books-Ratings или подобное может совпасть с этим
    path = _find_by_keywords(must_contain=["book"], must_not_contain=["rating"])
    sep, enc = _sniff_dialect(path)

    logger.info("[BX] чтение books %s ...", path.name)
    df = pd.read_csv(path, sep=sep, encoding=enc, on_bad_lines="skip",
                     dtype=str, low_memory=False)
    df.columns = [c.strip().lower().replace("-", "_") for c in df.columns]
    df = df.rename(columns={"isbn": "book_id", "book_title": "title", "book_author": "author"})

    keep = [c for c in ("book_id", "title", "author") if c in df.columns]
    df = df[keep].drop_duplicates(subset=["book_id"])
    logger.info("BX books: загружено %d книг с метаданными", len(df))
    return df.reset_index(drop=True)
