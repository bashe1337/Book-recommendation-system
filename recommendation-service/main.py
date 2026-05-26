"""FastAPI-микросервис рекомендаций (content-based filtering) для BRS.

Запуск:  uvicorn main:app --port 8001
При старте загружает данные из PostgreSQL и обучает модель.
"""

import logging
import os
import time

import numpy as np
import pandas as pd
from fastapi import FastAPI, HTTPException, Query, Response
from fastapi.responses import HTMLResponse

from data.bookcrossing import load_books_meta as load_bx_books
from data.bookcrossing import load_ratings as load_bx_ratings
from data.bookcrossing import rescale_ratings
from data.loader import load_all_interactions, load_all_ratings, load_books, load_user_ratings
from models.als import ALSModel
from models.collaborative import UserBasedCFModel, filter_ratings, per_user_train_test_split
from models.item_based import ItemBasedCFModel
from models.content_based import ContentBasedModel
from models.metrics import compute_implicit_metrics, compute_metrics, make_chart_png, make_ranking_chart_png
from models.svd_model import SVDModel

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)
logger = logging.getLogger("recommendation-service")

app = FastAPI(title="BRS Recommendation Service", version="1.0.0")

# Единственный экземпляр модели на процесс. Переобучается через /retrain.
model = ContentBasedModel()

# Состояние модулей CF (user-based и item-based).
# Держим в module-level — модели тяжёлые, переобучаются явно через POST.
cf_model = UserBasedCFModel()
cf_metrics_cache: dict = {}
cf_chart_cache: bytes | None = None
cf_books_meta: pd.DataFrame | None = None

ib_cf_model = ItemBasedCFModel()
ib_cf_metrics_cache: dict = {}
ib_cf_chart_cache: bytes | None = None
ib_cf_books_meta: pd.DataFrame | None = None

als_model = ALSModel()
als_metrics_cache: dict = {}
als_chart_cache: bytes | None = None
als_books_meta: pd.DataFrame | None = None

svd_model = SVDModel()
svd_metrics_cache: dict = {}
svd_chart_cache: bytes | None = None
svd_books_meta: pd.DataFrame | None = None


def _train_model() -> float:
    """Загружает книги и обучает модель. Возвращает время обучения в секундах."""
    started = time.perf_counter()
    books_df = load_books()
    model.fit(books_df)
    elapsed = time.perf_counter() - started
    logger.info(
        "Модель обучена: книг=%d, время=%.3f c", model.book_count, elapsed
    )
    return elapsed


@app.on_event("startup")
def on_startup() -> None:
    """При старте приложения автоматически загружаем данные и обучаем модель."""
    logger.info("Старт сервиса — инициализация модели...")
    try:
        _train_model()
        logger.info("Модель готова. Книг загружено: %d", model.book_count)
    except Exception:
        # Не валим процесс: сервис поднимется, /health покажет fitted=False,
        # модель можно дообучить через POST /retrain после починки БД.
        logger.exception("Не удалось обучить модель при старте")


@app.get("/health")
def health() -> dict:
    """Статус сервиса и количество книг в обученной модели."""
    return {
        "status": "ok",
        "model_fitted": model.is_fitted,
        "book_count": model.book_count,
    }


@app.get("/similar/{book_id}")
def similar(book_id: str, limit: int = Query(10, ge=1, le=100)) -> dict:
    """Похожие книги. 404, если книги нет в модели."""
    if not model.is_fitted:
        raise HTTPException(status_code=503, detail="Модель ещё не обучена")

    results = model.get_similar_books(book_id, n=limit)
    if not results and book_id not in model._id_to_index:
        raise HTTPException(status_code=404, detail="Книга не найдена в модели")

    return {"book_id": book_id, "similar": results}


@app.get("/recommendations/{user_id}")
def recommendations(user_id: str, limit: int = Query(10, ge=1, le=100)) -> dict:
    """Персональные рекомендации. 404, если у пользователя меньше 3 оценок."""
    if not model.is_fitted:
        raise HTTPException(status_code=503, detail="Модель ещё не обучена")

    rated = load_user_ratings(user_id)
    if len(rated) < 3:
        raise HTTPException(
            status_code=404, detail="Недостаточно данных для рекомендаций"
        )

    results = model.get_recommendations_for_user(rated, n=limit)
    return {"user_id": user_id, "recommendations": results}


@app.post("/retrain")
def retrain() -> dict:
    """Перезагружает данные из БД и переобучает модель."""
    logger.info("Запрошено переобучение модели...")
    try:
        elapsed = _train_model()
    except Exception as exc:
        logger.exception("Ошибка при переобучении")
        raise HTTPException(status_code=500, detail=f"Ошибка переобучения: {exc}")

    return {
        "book_count": model.book_count,
        "training_time_seconds": round(elapsed, 3),
    }


# ============================================================================
# User-based Collaborative Filtering
# ============================================================================

# Дефолтный порог релевантности зависит от шкалы:
#   1..10 (BX)  -> 7   (≈ "хорошо или лучше")
#   1..5  (BRS) -> 4   (≈ "хорошо или лучше")
_DEFAULT_THRESHOLD = {10: 7.0, 5: 4.0}


def _run_training(
    model,
    ratings: pd.DataFrame,
    rating_scale: int,
    min_user_ratings: int,
    min_book_ratings: int,
    test_size: float,
    relevance_threshold: float | None,
    sample_users: int,
    chart_title: str,
) -> tuple[dict, bytes, float]:
    """Универсальный пайплайн обучения CF (работает и для user-based, и для item-based).

    Правильный порядок шагов:
      1) filter_ratings  — оставляем только активных юзеров/книги
      2) per_user_split  — гарантируем что каждый юзер есть и в train, и в test
      3) fit(prefiltered=True) — обучение на train без повторной фильтрации
      4) compute_metrics — test содержит только юзеров/книги из модели → корректные P@K / R@K

    Возвращает (metrics, chart_png, elapsed_seconds). Глобалы заполняет вызывающий код.
    """
    started = time.perf_counter()
    threshold = relevance_threshold if relevance_threshold is not None \
        else _DEFAULT_THRESHOLD.get(rating_scale, 0.7 * rating_scale)

    # ШАГ 1: фильтрация ДО split — и train, и test будут из одной вселенной
    logger.info("[CF] Фильтрация перед split (min_user=%d, min_book=%d)...",
                min_user_ratings, min_book_ratings)
    filtered = filter_ratings(ratings, min_user_ratings, min_book_ratings)
    if filtered.empty:
        raise HTTPException(
            status_code=400,
            detail="После фильтрации не осталось данных — снизьте min_user_ratings/min_book_ratings",
        )

    # ШАГ 2: per-user split — каждый юзер попадает в оба сета
    train_df, test_df = per_user_train_test_split(filtered, test_size=test_size)
    logger.info("[CF] scale=1..%d, threshold=%.1f", rating_scale, threshold)

    # ШАГ 3: обучение (prefiltered=True — фильтр не запускается повторно)
    try:
        model.fit(train_df, prefiltered=True)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc))

    # ШАГ 4: метрики на test
    metrics = compute_metrics(
        model, test_df, k=10,
        relevance_threshold=threshold,
        sample_users=sample_users,
        rating_scale=rating_scale,
    )
    chart = make_chart_png(metrics, title=chart_title)

    elapsed = round(time.perf_counter() - started, 2)
    logger.info("[CF] обучение и оценка завершены за %.2f c", elapsed)
    return metrics, chart, elapsed


@app.post("/cf/train")
def cf_train(
    k_neighbors: int = Query(30, ge=1, le=200),
    min_user_ratings: int = Query(10, ge=1, le=200),
    min_book_ratings: int = Query(20, ge=1, le=500),
    test_size: float = Query(0.2, gt=0.0, lt=0.9),
    rating_scale: int = Query(5, description="Целевая шкала оценок: 5 (как в BRS) или 10 (нативная BX)"),
    relevance_threshold: float | None = Query(None, description="Порог релевантности; если не задан — авто (7 для шкалы 10, 4 для шкалы 5)"),
    sample_users: int = Query(300, ge=10, le=5000),
) -> dict:
    """Обучает User-based CF на датасете Book-Crossing.

    Bx содержит оценки в шкале 1..10. По умолчанию (`rating_scale=5`) они
    линейно пересчитываются в шкалу 1..5, чтобы совпадать с BRS — тогда
    RMSE/MAE интерпретируются в той же шкале, что и в продакшене.
    Поставьте `rating_scale=10`, если хочется бенчмарка в нативной BX-шкале.
    """
    global cf_books_meta

    if rating_scale not in (5, 10):
        raise HTTPException(status_code=400, detail="rating_scale должен быть 5 или 10")

    logger.info("[CF] === НАЧАЛО /cf/train (target_scale=1..%d) ===", rating_scale)
    t_load = time.perf_counter()
    try:
        ratings = load_bx_ratings()
        cf_books_meta = load_bx_books()
    except FileNotFoundError as exc:
        raise HTTPException(status_code=500, detail=str(exc))
    logger.info("[CF] датасет загружен за %.2f c (%d оценок, %d книг в метаданных)",
                time.perf_counter() - t_load, len(ratings),
                len(cf_books_meta) if cf_books_meta is not None else 0)

    ratings = rescale_ratings(ratings, target_max=rating_scale, source_max=10)

    global cf_model, cf_metrics_cache, cf_chart_cache
    cf_model = UserBasedCFModel(
        k_neighbors=k_neighbors,
        min_user_ratings=min_user_ratings,
        min_book_ratings=min_book_ratings,
    )
    metrics, chart, elapsed = _run_training(
        model=cf_model,
        ratings=ratings,
        rating_scale=rating_scale,
        min_user_ratings=min_user_ratings,
        min_book_ratings=min_book_ratings,
        test_size=test_size,
        relevance_threshold=relevance_threshold,
        sample_users=sample_users,
        chart_title="User-based Collaborative Filtering — метрики качества",
    )
    cf_metrics_cache = metrics
    cf_chart_cache = chart
    return {**cf_model.stats, **metrics, "training_time_seconds": elapsed}


@app.post("/cf/train-local")
def cf_train_local(
    k_neighbors: int = Query(30, ge=1, le=200),
    min_user_ratings: int = Query(3, ge=1, le=200),
    min_book_ratings: int = Query(3, ge=1, le=500),
    test_size: float = Query(0.2, gt=0.0, lt=0.9),
    relevance_threshold: float = Query(4.0, ge=1.0, le=5.0),
    sample_users: int = Query(300, ge=10, le=5000),
) -> dict:
    """Обучает CF на реальной таблице `ratings` из BRS Postgres (шкала 1..5).

    Полезно после того, как наберётся достаточный объём оценок в BRS —
    тогда модель будет рекомендовать вашим пользователям ваши книги.
    Если оценок мало — фильтры (min_user_ratings, min_book_ratings)
    отбросят почти всё; снижайте пороги до 2..3.
    """
    global cf_books_meta

    logger.info("CF: загрузка оценок из BRS Postgres...")
    try:
        ratings = load_all_ratings()
    except Exception as exc:
        raise HTTPException(status_code=500, detail=f"Не удалось загрузить ratings: {exc}")

    if ratings.empty:
        raise HTTPException(status_code=400, detail="В таблице ratings нет данных")

    # Для совместимости с /cf/recommendations отключаем BX-метаданные,
    # т.к. book_id здесь — Guid BRS, а не ISBN.
    cf_books_meta = None

    global cf_model, cf_metrics_cache, cf_chart_cache
    cf_model = UserBasedCFModel(
        k_neighbors=k_neighbors,
        min_user_ratings=min_user_ratings,
        min_book_ratings=min_book_ratings,
    )
    metrics, chart, elapsed = _run_training(
        model=cf_model,
        ratings=ratings,
        rating_scale=5,
        min_user_ratings=min_user_ratings,
        min_book_ratings=min_book_ratings,
        test_size=test_size,
        relevance_threshold=relevance_threshold,
        sample_users=sample_users,
        chart_title="User-based Collaborative Filtering (BRS) — метрики качества",
    )
    cf_metrics_cache = metrics
    cf_chart_cache = chart
    return {**cf_model.stats, **metrics, "training_time_seconds": elapsed}


@app.get("/cf/recommendations/{user_id}")
def cf_recommendations(user_id: str, limit: int = Query(10, ge=1, le=100)) -> dict:
    """Персональные рекомендации по User-based CF.

    user_id принимается как строка, но мы пробуем подобрать тип под обученную
    модель: BX использует int, локальная BRS-модель — Guid-строку.
    """
    if not cf_model.is_fitted:
        raise HTTPException(status_code=503, detail="CF-модель не обучена — вызовите POST /cf/train")

    # Пробуем варианты — модель хранит id в исходном типе из train DataFrame.
    candidates: list = [user_id]
    try:
        candidates.append(int(user_id))
    except (ValueError, TypeError):
        pass

    recs: list = []
    for cand in candidates:
        recs = cf_model.recommend(cand, n=limit)
        if recs:
            break

    if not recs:
        raise HTTPException(status_code=404, detail="Пользователь не найден в обучающей выборке")

    # Обогащаем метаданными (название/автор), если они загружены.
    if cf_books_meta is not None and not cf_books_meta.empty:
        meta_idx = cf_books_meta.set_index("book_id")
        for r in recs:
            if r["book_id"] in meta_idx.index:
                row = meta_idx.loc[r["book_id"]]
                # На случай дубликатов ISBN .loc вернёт DataFrame — берём первый
                if isinstance(row, pd.DataFrame):
                    row = row.iloc[0]
                r["title"] = row.get("title")
                r["author"] = row.get("author")

    return {"user_id": user_id, "recommendations": recs}


@app.get("/cf/metrics")
def cf_metrics() -> dict:
    """Возвращает таблицу метрик в JSON. 404, если /cf/train ещё не вызван."""
    if not cf_metrics_cache:
        raise HTTPException(status_code=404, detail="Метрики ещё не посчитаны — вызовите POST /cf/train")
    return cf_metrics_cache


@app.get("/cf/metrics/chart.png")
def cf_metrics_chart() -> Response:
    """PNG-график с двумя bar chart: RMSE/MAE и Precision@10/Recall@10."""
    if cf_chart_cache is None:
        raise HTTPException(status_code=404, detail="Графика нет — вызовите POST /cf/train")
    return Response(content=cf_chart_cache, media_type="image/png")


@app.get("/cf/metrics/report", response_class=HTMLResponse)
def cf_metrics_report() -> HTMLResponse:
    """HTML-отчёт: таблица метрик + встроенный график (для просмотра в браузере)."""
    if not cf_metrics_cache:
        raise HTTPException(status_code=404, detail="Метрики ещё не посчитаны — вызовите POST /cf/train")

    return HTMLResponse(content=_metrics_report_html(
        cf_metrics_cache,
        title="User-based Collaborative Filtering — метрики качества",
        chart_url="/cf/metrics/chart.png",
    ))


def _metrics_report_html(m: dict, title: str, chart_url: str) -> str:
    """Собирает HTML-страницу с таблицей метрик и встроенным графиком."""
    k = m["k"]
    return f"""<!doctype html>
<html lang="ru">
<head>
  <meta charset="utf-8">
  <title>{title}</title>
  <style>
    body {{ font-family: -apple-system, Segoe UI, Roboto, sans-serif;
            max-width: 960px; margin: 2rem auto; padding: 0 1rem; color: #222; }}
    h1 {{ font-size: 1.4rem; }}
    table {{ border-collapse: collapse; margin: 1rem 0; min-width: 360px; }}
    th, td {{ border: 1px solid #ccc; padding: 8px 14px; text-align: left; }}
    th {{ background: #f4f4f4; }}
    .meta {{ color: #666; font-size: 0.9rem; }}
    img {{ margin-top: 1rem; max-width: 100%; border: 1px solid #eee; }}
  </style>
</head>
<body>
  <h1>{title}</h1>
  <table>
    <tr><th>Метрика</th><th>Значение</th></tr>
    <tr><td>RMSE</td><td>{m['rmse']:.4f}</td></tr>
    <tr><td>MAE</td><td>{m['mae']:.4f}</td></tr>
    <tr><td>Precision@{k}</td><td>{m[f'precision_at_{k}']:.4f}</td></tr>
    <tr><td>Recall@{k}</td><td>{m[f'recall_at_{k}']:.4f}</td></tr>
  </table>
  <p class="meta">
    Тестовых предсказаний: {m['test_predictions']},
    порог релевантности: {m['relevance_threshold']}
  </p>
  <img src="{chart_url}" alt="metrics chart">
</body>
</html>"""


# ============================================================================
# Item-based Collaborative Filtering
# ============================================================================

@app.post("/item-cf/train")
def ib_cf_train(
    k_neighbors: int = Query(30, ge=1, le=200),
    min_user_ratings: int = Query(10, ge=1, le=200),
    min_book_ratings: int = Query(20, ge=1, le=500),
    test_size: float = Query(0.2, gt=0.0, lt=0.9),
    rating_scale: int = Query(5, description="Целевая шкала оценок: 5 (как в BRS) или 10 (нативная BX)"),
    relevance_threshold: float | None = Query(None, description="Порог релевантности; если не задан — авто (7 для шкалы 10, 4 для шкалы 5)"),
    sample_users: int = Query(300, ge=10, le=5000),
) -> dict:
    """Обучает Item-based CF на датасете Book-Crossing.

    Item-based отличается от user-based тем, что считается похожесть КНИГ
    между собой (а не пользователей), и предсказание для книги i = взвешенное
    среднее оценок пользователя на похожие книги. Часто стабильнее
    user-based на разреженных данных и легче объясняется ("вам понравилась
    X — посмотрите на Y").
    """
    global ib_cf_books_meta, ib_cf_model, ib_cf_metrics_cache, ib_cf_chart_cache

    if rating_scale not in (5, 10):
        raise HTTPException(status_code=400, detail="rating_scale должен быть 5 или 10")

    logger.info("[IB-CF] === НАЧАЛО /item-cf/train (target_scale=1..%d) ===", rating_scale)
    t_load = time.perf_counter()
    try:
        ratings = load_bx_ratings()
        ib_cf_books_meta = load_bx_books()
    except FileNotFoundError as exc:
        raise HTTPException(status_code=500, detail=str(exc))
    logger.info("[IB-CF] датасет загружен за %.2f c (%d оценок, %d книг в метаданных)",
                time.perf_counter() - t_load, len(ratings),
                len(ib_cf_books_meta) if ib_cf_books_meta is not None else 0)

    ratings = rescale_ratings(ratings, target_max=rating_scale, source_max=10)

    ib_cf_model = ItemBasedCFModel(
        k_neighbors=k_neighbors,
        min_user_ratings=min_user_ratings,
        min_book_ratings=min_book_ratings,
    )
    metrics, chart, elapsed = _run_training(
        model=ib_cf_model,
        ratings=ratings,
        rating_scale=rating_scale,
        min_user_ratings=min_user_ratings,
        min_book_ratings=min_book_ratings,
        test_size=test_size,
        relevance_threshold=relevance_threshold,
        sample_users=sample_users,
        chart_title="Item-based Collaborative Filtering — метрики качества",
    )
    ib_cf_metrics_cache = metrics
    ib_cf_chart_cache = chart
    return {**ib_cf_model.stats, **metrics, "training_time_seconds": elapsed}


@app.post("/item-cf/train-local")
def ib_cf_train_local(
    k_neighbors: int = Query(30, ge=1, le=200),
    min_user_ratings: int = Query(3, ge=1, le=200),
    min_book_ratings: int = Query(3, ge=1, le=500),
    test_size: float = Query(0.2, gt=0.0, lt=0.9),
    relevance_threshold: float = Query(4.0, ge=1.0, le=5.0),
    sample_users: int = Query(300, ge=10, le=5000),
) -> dict:
    """Обучает Item-based CF на реальной таблице `ratings` из BRS Postgres (шкала 1..5)."""
    global ib_cf_books_meta, ib_cf_model, ib_cf_metrics_cache, ib_cf_chart_cache

    logger.info("[IB-CF] загрузка оценок из BRS Postgres...")
    try:
        ratings = load_all_ratings()
    except Exception as exc:
        raise HTTPException(status_code=500, detail=f"Не удалось загрузить ratings: {exc}")

    if ratings.empty:
        raise HTTPException(status_code=400, detail="В таблице ratings нет данных")

    ib_cf_books_meta = None  # BRS использует Guid, не ISBN — метаданные BX не подойдут

    ib_cf_model = ItemBasedCFModel(
        k_neighbors=k_neighbors,
        min_user_ratings=min_user_ratings,
        min_book_ratings=min_book_ratings,
    )
    metrics, chart, elapsed = _run_training(
        model=ib_cf_model,
        ratings=ratings,
        rating_scale=5,
        min_user_ratings=min_user_ratings,
        min_book_ratings=min_book_ratings,
        test_size=test_size,
        relevance_threshold=relevance_threshold,
        sample_users=sample_users,
        chart_title="Item-based Collaborative Filtering (BRS) — метрики качества",
    )
    ib_cf_metrics_cache = metrics
    ib_cf_chart_cache = chart
    return {**ib_cf_model.stats, **metrics, "training_time_seconds": elapsed}


@app.get("/item-cf/recommendations/{user_id}")
def ib_cf_recommendations(user_id: str, limit: int = Query(10, ge=1, le=100)) -> dict:
    """Персональные рекомендации по Item-based CF.

    user_id принимается как строка, но мы пробуем подобрать тип под обученную
    модель: BX использует int, локальная BRS-модель — Guid-строку.
    """
    if not ib_cf_model.is_fitted:
        raise HTTPException(status_code=503, detail="Item-CF не обучена — вызовите POST /item-cf/train")

    candidates: list = [user_id]
    try:
        candidates.append(int(user_id))
    except (ValueError, TypeError):
        pass

    recs: list = []
    for cand in candidates:
        recs = ib_cf_model.recommend(cand, n=limit)
        if recs:
            break

    if not recs:
        raise HTTPException(status_code=404, detail="Пользователь не найден в обучающей выборке")

    if ib_cf_books_meta is not None and not ib_cf_books_meta.empty:
        meta_idx = ib_cf_books_meta.set_index("book_id")
        for r in recs:
            if r["book_id"] in meta_idx.index:
                row = meta_idx.loc[r["book_id"]]
                if isinstance(row, pd.DataFrame):
                    row = row.iloc[0]
                r["title"] = row.get("title")
                r["author"] = row.get("author")

    return {"user_id": user_id, "recommendations": recs}


@app.get("/item-cf/similar/{book_id}")
def ib_cf_similar(book_id: str, limit: int = Query(10, ge=1, le=100)) -> dict:
    """Похожие книги по item-based CF (cosine на adjusted-centered матрице).

    В отличие от /similar/{book_id} (content-based на TF-IDF), здесь похожесть
    рассчитана по совпадению поведения пользователей: книги, которые часто
    оценивают одни и те же люди — похожи. Use case: блок "С этой книгой читают"
    на странице конкретной книги.
    """
    if not ib_cf_model.is_fitted:
        raise HTTPException(status_code=503, detail="Item-CF не обучена — вызовите POST /item-cf/train-local")

    # причина: модель хранит book_id в исходном типе (Guid-строка для BRS,
    # ISBN-строка для BX) — пробуем оба варианта
    candidates: list = [book_id]
    try:
        candidates.append(int(book_id))
    except (ValueError, TypeError):
        pass

    idx = None
    for cand in candidates:
        idx = ib_cf_model._index.book_to_idx.get(cand) if ib_cf_model._index else None
        if idx is not None:
            break

    if idx is None:
        raise HTTPException(status_code=404, detail="Книга не найдена в обученной модели")

    # причина: similarity_T хранит top-k соседей в .T формате — столбец idx
    # содержит похожесть item idx ко всем остальным; .T → строка idx исходной similarity
    row = ib_cf_model._similarity.getrow(idx).toarray().ravel()
    # Сортируем по убыванию похожести (отрицательные — анти-корреляция, отбрасываем)
    order = np.argsort(-row)
    book_ids = ib_cf_model._index.book_ids
    results: list[dict] = []
    for pos in order:
        score = float(row[pos])
        if score <= 0 or pos == idx:
            continue
        results.append({"book_id": book_ids[pos], "similarity_score": score})
        if len(results) >= limit:
            break

    return {"book_id": book_id, "similar": results}


@app.get("/item-cf/metrics")
def ib_cf_metrics() -> dict:
    """Таблица метрик item-based в JSON. 404, если /item-cf/train ещё не вызван."""
    if not ib_cf_metrics_cache:
        raise HTTPException(status_code=404, detail="Метрики ещё не посчитаны — вызовите POST /item-cf/train")
    return ib_cf_metrics_cache


@app.get("/item-cf/metrics/chart.png")
def ib_cf_metrics_chart() -> Response:
    """PNG-график метрик item-based модели."""
    if ib_cf_chart_cache is None:
        raise HTTPException(status_code=404, detail="Графика нет — вызовите POST /item-cf/train")
    return Response(content=ib_cf_chart_cache, media_type="image/png")


@app.get("/item-cf/metrics/report", response_class=HTMLResponse)
def ib_cf_metrics_report() -> HTMLResponse:
    """HTML-отчёт item-based: таблица метрик + встроенный график."""
    if not ib_cf_metrics_cache:
        raise HTTPException(status_code=404, detail="Метрики ещё не посчитаны — вызовите POST /item-cf/train")
    return HTMLResponse(content=_metrics_report_html(
        ib_cf_metrics_cache,
        title="Item-based Collaborative Filtering — метрики качества",
        chart_url="/item-cf/metrics/chart.png",
    ))


# ============================================================================
# ALS (matrix factorization для implicit feedback)
# ============================================================================

def _run_als_training(
    interactions: pd.DataFrame,
    factors: int,
    regularization: float,
    alpha: float,
    iterations: int,
    min_user_interactions: int,
    min_book_interactions: int,
    test_size: float,
    sample_users: int,
    chart_title: str,
) -> tuple[dict, bytes, float]:
    """Универсальный пайплайн обучения ALS.

    Шаги те же, что для CF: filter → per_user_split → fit(prefiltered=True)
    → compute_implicit_metrics → chart.
    Возвращает (metrics, chart_png, elapsed_seconds).
    """
    started = time.perf_counter()

    logger.info("[ALS] Фильтрация перед split (min_user=%d, min_book=%d)...",
                min_user_interactions, min_book_interactions)
    filtered = filter_ratings(interactions, min_user_interactions, min_book_interactions)
    if filtered.empty:
        raise HTTPException(
            status_code=400,
            detail="После фильтрации не осталось данных — снизьте min_*_interactions",
        )

    train_df, test_df = per_user_train_test_split(filtered, test_size=test_size)

    # Глобальный als_model уже сконструирован в вызывающем коде с нужными гиперпарамами.
    try:
        als_model.fit(train_df, prefiltered=True)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc))

    metrics = compute_implicit_metrics(
        als_model,
        als_model._user_items,   # csr матрица из train — для filter_already_liked
        test_df, k=10,
        sample_users=sample_users,
    )
    chart = make_ranking_chart_png(metrics, title=chart_title)

    elapsed = round(time.perf_counter() - started, 2)
    logger.info("[ALS] обучение и оценка завершены за %.2f c", elapsed)
    return metrics, chart, elapsed


@app.post("/als/train")
def als_train(
    factors: int = Query(64, ge=8, le=512, description="Размерность латентного пространства"),
    regularization: float = Query(0.01, ge=0.0, le=10.0),
    alpha: float = Query(15.0, ge=0.1, le=100.0, description="Множитель confidence: c = 1 + α·signal"),
    iterations: int = Query(15, ge=1, le=200),
    min_user_interactions: int = Query(3, ge=1, le=200),
    min_book_interactions: int = Query(3, ge=1, le=500),
    test_size: float = Query(0.2, gt=0.0, lt=0.9),
    rating_scale: int = Query(5, description="Целевая шкала: 5 (как в BRS) или 10 (нативная BX)"),
    sample_users: int = Query(300, ge=10, le=5000),
) -> dict:
    """Обучает ALS на BX (explicit ratings как confidence-сигналы).

    Для BX 'confidence' = значение оценки. Это валидное использование implicit
    ALS на explicit данных (см. Hu et al.) — оценка просто становится сильным
    сигналом интереса.
    """
    global als_books_meta, als_model, als_metrics_cache, als_chart_cache

    if rating_scale not in (5, 10):
        raise HTTPException(status_code=400, detail="rating_scale должен быть 5 или 10")

    logger.info("[ALS] === НАЧАЛО /als/train (target_scale=1..%d) ===", rating_scale)
    t_load = time.perf_counter()
    try:
        ratings = load_bx_ratings()
        als_books_meta = load_bx_books()
    except FileNotFoundError as exc:
        raise HTTPException(status_code=500, detail=str(exc))
    logger.info("[ALS] датасет загружен за %.2f c (%d оценок)",
                time.perf_counter() - t_load, len(ratings))

    ratings = rescale_ratings(ratings, target_max=rating_scale, source_max=10)
    # ALS ждёт колонку confidence — переименовываем rating.
    ratings = ratings.rename(columns={"rating": "confidence"})

    als_model = ALSModel(
        factors=factors,
        regularization=regularization,
        alpha=alpha,
        iterations=iterations,
        min_user_interactions=min_user_interactions,
        min_book_interactions=min_book_interactions,
    )
    metrics, chart, elapsed = _run_als_training(
        interactions=ratings,
        factors=factors,
        regularization=regularization,
        alpha=alpha,
        iterations=iterations,
        min_user_interactions=min_user_interactions,
        min_book_interactions=min_book_interactions,
        test_size=test_size,
        sample_users=sample_users,
        chart_title="ALS на Book-Crossing — метрики качества",
    )
    als_metrics_cache = metrics
    als_chart_cache = chart
    return {**als_model.stats, **metrics, "training_time_seconds": elapsed}


@app.post("/als/train-local")
def als_train_local(
    factors: int = Query(64, ge=8, le=512),
    regularization: float = Query(0.01, ge=0.0, le=10.0),
    alpha: float = Query(15.0, ge=0.1, le=100.0),
    iterations: int = Query(15, ge=1, le=200),
    min_user_interactions: int = Query(2, ge=1, le=200),
    min_book_interactions: int = Query(2, ge=1, le=500),
    test_size: float = Query(0.2, gt=0.0, lt=0.9),
    weight_view: float = Query(1.0, ge=0.0, le=100.0),
    weight_favorite: float = Query(4.0, ge=0.0, le=100.0),
    weight_rating: float = Query(1.0, ge=0.0, le=100.0,
                                 description="Множитель score (итоговый вес = score × multiplier)"),
    sample_users: int = Query(300, ge=10, le=5000),
) -> dict:
    """Обучает ALS на реальных взаимодействиях BRS (views + favorites + ratings).

    Это **главный production-сценарий** ALS. Объединяет три источника сигналов
    в единую confidence-матрицу:
      • view_history (просмотры) — слабый сигнал
      • favorites (избранное)    — средний сигнал
      • ratings (1..5)           — взвешенный явный сигнал

    Веса параметризованы. По дефолту: view=1, favorite=4, rating × 1 (т.е. 1..5).
    """
    global als_books_meta, als_model, als_metrics_cache, als_chart_cache

    logger.info("[ALS] === НАЧАЛО /als/train-local "
                "(weights: view=%.1f, fav=%.1f, rating×%.1f) ===",
                weight_view, weight_favorite, weight_rating)
    try:
        interactions = load_all_interactions(
            weight_view=weight_view,
            weight_favorite=weight_favorite,
            weight_rating_multiplier=weight_rating,
        )
    except Exception as exc:
        raise HTTPException(status_code=500, detail=f"Не удалось загрузить взаимодействия: {exc}")

    if interactions.empty:
        raise HTTPException(
            status_code=400,
            detail="В BRS нет взаимодействий (views/favorites/ratings пусты). "
                   "Засейте данные или дождитесь активности пользователей.",
        )

    als_books_meta = None  # BRS использует Guid, не ISBN — BX-метаданные не подойдут

    als_model = ALSModel(
        factors=factors,
        regularization=regularization,
        alpha=alpha,
        iterations=iterations,
        min_user_interactions=min_user_interactions,
        min_book_interactions=min_book_interactions,
    )
    metrics, chart, elapsed = _run_als_training(
        interactions=interactions,
        factors=factors,
        regularization=regularization,
        alpha=alpha,
        iterations=iterations,
        min_user_interactions=min_user_interactions,
        min_book_interactions=min_book_interactions,
        test_size=test_size,
        sample_users=sample_users,
        chart_title="ALS на BRS (production) — метрики качества",
    )
    als_metrics_cache = metrics
    als_chart_cache = chart
    return {**als_model.stats, **metrics, "training_time_seconds": elapsed}


@app.get("/als/recommendations/{user_id}")
def als_recommendations(user_id: str, limit: int = Query(10, ge=1, le=100)) -> dict:
    """Персональные рекомендации ALS.

    Принимает user_id как строку, пробует int (для BX) и оригинальный str (для BRS Guid).
    """
    if not als_model.is_fitted:
        raise HTTPException(status_code=503, detail="ALS не обучена — вызовите POST /als/train")

    candidates: list = [user_id]
    try:
        candidates.append(int(user_id))
    except (ValueError, TypeError):
        pass

    recs: list = []
    for cand in candidates:
        recs = als_model.recommend(cand, n=limit)
        if recs:
            break

    if not recs:
        raise HTTPException(status_code=404, detail="Пользователь не найден в обучающей выборке")

    if als_books_meta is not None and not als_books_meta.empty:
        meta_idx = als_books_meta.set_index("book_id")
        for r in recs:
            if r["book_id"] in meta_idx.index:
                row = meta_idx.loc[r["book_id"]]
                if isinstance(row, pd.DataFrame):
                    row = row.iloc[0]
                r["title"] = row.get("title")
                r["author"] = row.get("author")

    return {"user_id": user_id, "recommendations": recs}


@app.get("/als/metrics")
def als_metrics() -> dict:
    """Таблица метрик ALS в JSON. 404, если /als/train ещё не вызван."""
    if not als_metrics_cache:
        raise HTTPException(status_code=404, detail="Метрики ещё не посчитаны — вызовите POST /als/train")
    return als_metrics_cache


@app.get("/als/metrics/chart.png")
def als_metrics_chart() -> Response:
    """PNG-график метрик ALS (Precision@10 / Recall@10 / NDCG@10)."""
    if als_chart_cache is None:
        raise HTTPException(status_code=404, detail="Графика нет — вызовите POST /als/train")
    return Response(content=als_chart_cache, media_type="image/png")


@app.get("/als/metrics/report", response_class=HTMLResponse)
def als_metrics_report() -> HTMLResponse:
    """HTML-отчёт ALS: таблица метрик + встроенный график."""
    if not als_metrics_cache:
        raise HTTPException(status_code=404, detail="Метрики ещё не посчитаны — вызовите POST /als/train")
    m = als_metrics_cache
    k = m["k"]
    html = f"""<!doctype html>
<html lang="ru">
<head>
  <meta charset="utf-8">
  <title>ALS Metrics</title>
  <style>
    body {{ font-family: -apple-system, Segoe UI, Roboto, sans-serif;
            max-width: 960px; margin: 2rem auto; padding: 0 1rem; color: #222; }}
    h1 {{ font-size: 1.4rem; }}
    table {{ border-collapse: collapse; margin: 1rem 0; min-width: 360px; }}
    th, td {{ border: 1px solid #ccc; padding: 8px 14px; text-align: left; }}
    th {{ background: #f4f4f4; }}
    .meta {{ color: #666; font-size: 0.9rem; }}
    img {{ margin-top: 1rem; max-width: 100%; border: 1px solid #eee; }}
  </style>
</head>
<body>
  <h1>ALS (Alternating Least Squares) — метрики качества</h1>
  <table>
    <tr><th>Метрика</th><th>Значение</th></tr>
    <tr><td>Precision@{k}</td><td>{m[f'precision_at_{k}']:.4f}</td></tr>
    <tr><td>Recall@{k}</td><td>{m[f'recall_at_{k}']:.4f}</td></tr>
    <tr><td>NDCG@{k}</td><td>{m[f'ndcg_at_{k}']:.4f}</td></tr>
  </table>
  <p class="meta">
    Оценено по {m['evaluated_users']} пользователям.
    Для implicit feedback RMSE/MAE не применимы — там нет "правильной оценки".
  </p>
  <img src="/als/metrics/chart.png" alt="ALS metrics chart">
</body>
</html>"""
    return HTMLResponse(content=html)


# ============================================================================
# SVD (FunkSVD) — matrix factorization для explicit feedback
# ============================================================================

@app.post("/svd/train")
def svd_train(
    n_factors: int = Query(50, ge=8, le=512, description="Размерность латентного пространства"),
    n_epochs: int = Query(20, ge=1, le=200, description="Проходов SGD по датасету"),
    lr: float = Query(0.005, ge=0.0001, le=0.1, description="Learning rate"),
    reg: float = Query(0.02, ge=0.0, le=1.0, description="L2-регуляризация"),
    min_user_ratings: int = Query(10, ge=1, le=200),
    min_book_ratings: int = Query(20, ge=1, le=500),
    test_size: float = Query(0.2, gt=0.0, lt=0.9),
    rating_scale: int = Query(5, description="Целевая шкала: 5 (как в BRS) или 10 (нативная BX)"),
    relevance_threshold: float | None = Query(None, description="Порог релевантности; если не задан — авто (7 для 10, 4 для 5)"),
    sample_users: int = Query(300, ge=10, le=5000),
) -> dict:
    """Обучает FunkSVD на Book-Crossing.

    SVD — model-based подход для **явных оценок**. В отличие от ALS implicit,
    напрямую минимизирует ошибку предсказания рейтинга → RMSE/MAE
    интерпретируются нативно. Для BX обычно лучшая модель по RMSE.
    """
    global svd_books_meta, svd_model, svd_metrics_cache, svd_chart_cache

    if rating_scale not in (5, 10):
        raise HTTPException(status_code=400, detail="rating_scale должен быть 5 или 10")

    logger.info("[SVD] === НАЧАЛО /svd/train (target_scale=1..%d) ===", rating_scale)
    t_load = time.perf_counter()
    try:
        ratings = load_bx_ratings()
        svd_books_meta = load_bx_books()
    except FileNotFoundError as exc:
        raise HTTPException(status_code=500, detail=str(exc))
    logger.info("[SVD] датасет загружен за %.2f c (%d оценок)",
                time.perf_counter() - t_load, len(ratings))

    ratings = rescale_ratings(ratings, target_max=rating_scale, source_max=10)

    svd_model = SVDModel(
        n_factors=n_factors,
        n_epochs=n_epochs,
        lr=lr,
        reg=reg,
        min_user_ratings=min_user_ratings,
        min_book_ratings=min_book_ratings,
        rating_scale=(1.0, float(rating_scale)),
    )
    metrics, chart, elapsed = _run_training(
        model=svd_model,
        ratings=ratings,
        rating_scale=rating_scale,
        min_user_ratings=min_user_ratings,
        min_book_ratings=min_book_ratings,
        test_size=test_size,
        relevance_threshold=relevance_threshold,
        sample_users=sample_users,
        chart_title="SVD на Book-Crossing — метрики качества",
    )
    svd_metrics_cache = metrics
    svd_chart_cache = chart
    return {**svd_model.stats, **metrics, "training_time_seconds": elapsed}


@app.post("/svd/train-local")
def svd_train_local(
    n_factors: int = Query(50, ge=8, le=512),
    n_epochs: int = Query(20, ge=1, le=200),
    lr: float = Query(0.005, ge=0.0001, le=0.1),
    reg: float = Query(0.02, ge=0.0, le=1.0),
    min_user_ratings: int = Query(3, ge=1, le=200),
    min_book_ratings: int = Query(3, ge=1, le=500),
    test_size: float = Query(0.2, gt=0.0, lt=0.9),
    relevance_threshold: float = Query(4.0, ge=1.0, le=5.0),
    sample_users: int = Query(300, ge=10, le=5000),
) -> dict:
    """Обучает SVD на реальных оценках BRS Postgres (шкала 1..5)."""
    global svd_books_meta, svd_model, svd_metrics_cache, svd_chart_cache

    logger.info("[SVD] загрузка оценок из BRS Postgres...")
    try:
        ratings = load_all_ratings()
    except Exception as exc:
        raise HTTPException(status_code=500, detail=f"Не удалось загрузить ratings: {exc}")

    if ratings.empty:
        raise HTTPException(status_code=400, detail="В таблице ratings нет данных")

    svd_books_meta = None  # BRS использует Guid, не ISBN — BX-метаданные не подойдут

    svd_model = SVDModel(
        n_factors=n_factors,
        n_epochs=n_epochs,
        lr=lr,
        reg=reg,
        min_user_ratings=min_user_ratings,
        min_book_ratings=min_book_ratings,
        rating_scale=(1.0, 5.0),
    )
    metrics, chart, elapsed = _run_training(
        model=svd_model,
        ratings=ratings,
        rating_scale=5,
        min_user_ratings=min_user_ratings,
        min_book_ratings=min_book_ratings,
        test_size=test_size,
        relevance_threshold=relevance_threshold,
        sample_users=sample_users,
        chart_title="SVD на BRS (production) — метрики качества",
    )
    svd_metrics_cache = metrics
    svd_chart_cache = chart
    return {**svd_model.stats, **metrics, "training_time_seconds": elapsed}


@app.get("/svd/recommendations/{user_id}")
def svd_recommendations(user_id: str, limit: int = Query(10, ge=1, le=100)) -> dict:
    """Персональные рекомендации SVD.

    Принимает user_id как строку, пробует int (для BX) и оригинальный str
    (для BRS Guid) — модель хранит id в исходном типе.
    """
    if not svd_model.is_fitted:
        raise HTTPException(status_code=503, detail="SVD не обучена — вызовите POST /svd/train")

    candidates: list = [user_id]
    try:
        candidates.append(int(user_id))
    except (ValueError, TypeError):
        pass

    recs: list = []
    for cand in candidates:
        recs = svd_model.recommend(cand, n=limit)
        if recs:
            break

    if not recs:
        raise HTTPException(status_code=404, detail="Пользователь не найден в обучающей выборке")

    if svd_books_meta is not None and not svd_books_meta.empty:
        meta_idx = svd_books_meta.set_index("book_id")
        for r in recs:
            if r["book_id"] in meta_idx.index:
                row = meta_idx.loc[r["book_id"]]
                if isinstance(row, pd.DataFrame):
                    row = row.iloc[0]
                r["title"] = row.get("title")
                r["author"] = row.get("author")

    return {"user_id": user_id, "recommendations": recs}


@app.get("/svd/metrics")
def svd_metrics() -> dict:
    """Таблица метрик SVD в JSON. 404, если /svd/train ещё не вызван."""
    if not svd_metrics_cache:
        raise HTTPException(status_code=404, detail="Метрики ещё не посчитаны — вызовите POST /svd/train")
    return svd_metrics_cache


@app.get("/svd/metrics/chart.png")
def svd_metrics_chart() -> Response:
    """PNG-график метрик SVD (RMSE/MAE + Precision@10/Recall@10)."""
    if svd_chart_cache is None:
        raise HTTPException(status_code=404, detail="Графика нет — вызовите POST /svd/train")
    return Response(content=svd_chart_cache, media_type="image/png")


@app.get("/svd/metrics/report", response_class=HTMLResponse)
def svd_metrics_report() -> HTMLResponse:
    """HTML-отчёт SVD: таблица метрик + встроенный график."""
    if not svd_metrics_cache:
        raise HTTPException(status_code=404, detail="Метрики ещё не посчитаны — вызовите POST /svd/train")
    return HTMLResponse(content=_metrics_report_html(
        svd_metrics_cache,
        title="SVD (FunkSVD) — метрики качества",
        chart_url="/svd/metrics/chart.png",
    ))


if __name__ == "__main__":
    import uvicorn

    port = int(os.getenv("PORT", "8001"))
    uvicorn.run("main:app", host="0.0.0.0", port=port)
