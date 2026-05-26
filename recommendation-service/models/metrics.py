"""Метрики качества рекомендательной модели.

Считаем:
  - RMSE / MAE — ошибка предсказания оценки на тестовом сете;
  - Precision@K / Recall@K — качество топ-K рекомендаций
    (релевантной считаем книгу, чью реальную оценку пользователь поставил
    не ниже relevance_threshold).
"""

import io
import logging
import time

import numpy as np
import pandas as pd

logger = logging.getLogger(__name__)

# Каждые N обработанных пользователей пишем прогресс — без этого долгие
# циклы выглядят как "процесс висит".
_PROGRESS_EVERY = 200


def rmse(predictions: np.ndarray, actuals: np.ndarray) -> float:
    """Root Mean Squared Error — крупные ошибки штрафуются сильнее."""
    if len(predictions) == 0:
        return float("nan")
    return float(np.sqrt(np.mean((predictions - actuals) ** 2)))


def mae(predictions: np.ndarray, actuals: np.ndarray) -> float:
    """Mean Absolute Error — средняя абсолютная ошибка в шкале оценок."""
    if len(predictions) == 0:
        return float("nan")
    return float(np.mean(np.abs(predictions - actuals)))


def _per_user_predictions(model, test_df: pd.DataFrame) -> tuple[np.ndarray, np.ndarray]:
    """Эффективно: для каждого тестового пользователя один раз считаем вектор
    предсказаний по всем книгам и берём из него нужные элементы."""
    user_to_idx = model._index.user_to_idx
    book_to_idx = model._index.book_to_idx

    # Заранее считаем, сколько уникальных тестовых пользователей реально есть
    # в обученной модели — это даёт честный знаменатель для прогресса.
    test_users_in_model = sum(
        1 for uid in test_df["user_id"].unique() if uid in user_to_idx
    )
    logger.info(
        "[metrics] RMSE/MAE: %d тестовых записей, %d уникальных юзеров в модели",
        len(test_df), test_users_in_model,
    )

    preds, acts = [], []
    cache: dict = {}
    t0 = time.perf_counter()

    for row in test_df.itertuples(index=False):
        u = user_to_idx.get(row.user_id)
        b = book_to_idx.get(row.book_id)
        if u is None or b is None:
            continue
        if u not in cache:
            cache[u] = model.predict_all_for_user(u)
            # Прогресс по числу обсчитанных уникальных пользователей.
            done = len(cache)
            if done % _PROGRESS_EVERY == 0:
                elapsed = time.perf_counter() - t0
                rate = done / elapsed if elapsed > 0 else 0
                eta = (test_users_in_model - done) / rate if rate > 0 else 0
                logger.info(
                    "[metrics] RMSE/MAE: обработано %d/%d юзеров (%.1f/с, ETA %.0f c)",
                    done, test_users_in_model, rate, eta,
                )
        preds.append(cache[u][b])
        acts.append(row.rating)

    logger.info("[metrics] RMSE/MAE: predict для %d юзеров готов за %.2f c",
                len(cache), time.perf_counter() - t0)
    return np.array(preds), np.array(acts)


def precision_recall_at_k(
    model,
    test_df: pd.DataFrame,
    k: int = 10,
    relevance_threshold: float = 7.0,
    sample_users: int | None = 300,
    rng_seed: int = 42,
) -> tuple[float, float]:
    """Усреднённые Precision@K и Recall@K по пользователям тестового сета.

    sample_users — ограничение на число оцениваемых пользователей; полная оценка
    дорогая (для каждого пользователя — векторное предсказание по всем книгам).
    """
    all_test_users = test_df["user_id"].unique()

    # Оставляем только тех, кто реально есть в обученной модели.
    # После правильного pipeline (filter → per_user_split → fit) это будут
    # почти все юзеры; страховка нужна при нестандартном использовании.
    in_model = [u for u in all_test_users if u in model._index.user_to_idx]
    logger.info(
        "[metrics] Precision/Recall@%d: %d/%d тест-юзеров есть в модели",
        k, len(in_model), len(all_test_users),
    )
    if not in_model:
        logger.warning("[metrics] Нет тест-юзеров в модели — P@K=R@K=0. "
                       "Проверьте порядок filter → split → fit.")
        return 0.0, 0.0

    rng = np.random.default_rng(rng_seed)
    if sample_users and len(in_model) > sample_users:
        in_model = list(rng.choice(in_model, size=sample_users, replace=False))
    test_users = in_model

    logger.info("[metrics] Precision/Recall@%d: оцениваем по %d юзерам (порог релевантности %.1f)",
                k, len(test_users), relevance_threshold)

    precisions: list[float] = []
    recalls: list[float] = []

    # Группировка один раз — быстрее чем фильтровать test_df на каждого user
    test_by_user = test_df.groupby("user_id")
    t0 = time.perf_counter()
    processed = 0

    for uid in test_users:
        processed += 1
        if uid not in model._index.user_to_idx:
            continue
        try:
            user_test = test_by_user.get_group(uid)
        except KeyError:
            continue
        relevant = set(user_test[user_test["rating"] >= relevance_threshold]["book_id"])
        if not relevant:
            continue  # нет релевантных книг — recall не определён, пропускаем

        recs = model.recommend(uid, n=k)
        rec_ids = {r["book_id"] for r in recs}

        hits = len(rec_ids & relevant)
        precisions.append(hits / k)
        recalls.append(hits / len(relevant))

        if processed % _PROGRESS_EVERY == 0:
            elapsed = time.perf_counter() - t0
            rate = processed / elapsed if elapsed > 0 else 0
            eta = (len(test_users) - processed) / rate if rate > 0 else 0
            logger.info(
                "[metrics] Precision/Recall: %d/%d юзеров (%.1f/с, ETA %.0f c)",
                processed, len(test_users), rate, eta,
            )

    p = float(np.mean(precisions)) if precisions else 0.0
    r = float(np.mean(recalls)) if recalls else 0.0
    logger.info(
        "Precision@%d=%.4f, Recall@%d=%.4f (по %d пользователям)",
        k, p, k, r, len(precisions),
    )
    return p, r


def compute_metrics(
    model,
    test_df: pd.DataFrame,
    k: int = 10,
    relevance_threshold: float = 7.0,
    sample_users: int | None = 300,
    rating_scale: int = 10,
) -> dict:
    """Считает все четыре метрики и возвращает единым словарём."""
    total_start = time.perf_counter()

    logger.info("[metrics] === ФАЗА 1/2: RMSE/MAE ===")
    phase_start = time.perf_counter()
    preds, acts = _per_user_predictions(model, test_df)
    logger.info("[metrics] ФАЗА 1/2 завершена за %.2f c", time.perf_counter() - phase_start)

    logger.info("[metrics] === ФАЗА 2/2: Precision@%d / Recall@%d ===", k, k)
    phase_start = time.perf_counter()
    p_at_k, r_at_k = precision_recall_at_k(
        model, test_df, k=k, relevance_threshold=relevance_threshold,
        sample_users=sample_users,
    )
    logger.info("[metrics] ФАЗА 2/2 завершена за %.2f c", time.perf_counter() - phase_start)
    logger.info("[metrics] === ВСЕ МЕТРИКИ ГОТОВЫ за %.2f c ===", time.perf_counter() - total_start)

    return {
        "rmse": rmse(preds, acts),
        "mae": mae(preds, acts),
        f"precision_at_{k}": p_at_k,
        f"recall_at_{k}": r_at_k,
        "test_predictions": int(len(preds)),
        "k": k,
        "relevance_threshold": relevance_threshold,
        # Шкала оценок сохраняется в метриках, чтобы chart-рендер мог корректно
        # подписать ось и интерпретировать значения RMSE/MAE.
        "rating_scale": rating_scale,
    }


def ndcg_at_k(rec_ids: list, relevant_set: set, k: int) -> float:
    """Normalized Discounted Cumulative Gain @ K (binary relevance).

    DCG считает "правильные хиты с учётом позиции" — попадание на 1-й позиции
    весит сильнее, чем на 10-й (вес = 1/log2(pos+1)).
    IDCG — идеальный DCG (когда все top-K оказались релевантными).
    NDCG = DCG / IDCG ∈ [0, 1].

    Это стандарт для ranking-метрик в implicit feedback и любим комиссией ВКР.
    """
    if not relevant_set:
        return 0.0
    dcg = 0.0
    for pos, item in enumerate(rec_ids[:k]):
        if item in relevant_set:
            # pos с нуля; log2(2) = 1 на первой позиции
            dcg += 1.0 / np.log2(pos + 2)
    # IDCG: лучшее достижимое, когда первые min(|relevant|, k) позиций — попадания.
    n_relevant = min(len(relevant_set), k)
    idcg = sum(1.0 / np.log2(i + 2) for i in range(n_relevant))
    return dcg / idcg if idcg > 0 else 0.0


def compute_implicit_metrics(
    model,
    train_user_items,
    test_df: pd.DataFrame,
    k: int = 10,
    sample_users: int | None = 300,
    rng_seed: int = 42,
) -> dict:
    """Метрики ranking-качества для implicit feedback модели (ALS).

    Релевантной для пользователя считается любая книга из его test-сета
    (явный сигнал, скрытый при обучении). RMSE/MAE для implicit не считаются
    — там нет "правильной оценки", только бинарный "взаимодействовал/нет".
    """
    total_start = time.perf_counter()

    all_test_users = test_df["user_id"].unique()
    in_model = [u for u in all_test_users if u in model._index.user_to_idx]
    logger.info("[metrics-ALS] %d/%d тест-юзеров есть в модели",
                len(in_model), len(all_test_users))
    if not in_model:
        return {f"precision_at_{k}": 0.0, f"recall_at_{k}": 0.0,
                f"ndcg_at_{k}": 0.0, "evaluated_users": 0, "k": k}

    rng = np.random.default_rng(rng_seed)
    if sample_users and len(in_model) > sample_users:
        in_model = list(rng.choice(in_model, size=sample_users, replace=False))

    logger.info("[metrics-ALS] оцениваем по %d юзерам", len(in_model))

    test_by_user = test_df.groupby("user_id")
    precisions, recalls, ndcgs = [], [], []
    t0 = time.perf_counter()

    for processed, uid in enumerate(in_model, 1):
        u_idx = model._index.user_to_idx[uid]
        try:
            user_test = test_by_user.get_group(uid)
        except KeyError:
            continue

        # Релевантные = индексы книг из теста, известные модели
        relevant = set()
        for bid in user_test["book_id"]:
            b_idx = model._index.book_to_idx.get(bid)
            if b_idx is not None:
                relevant.add(b_idx)
        if not relevant:
            continue

        # implicit.recommend: первый аргумент — userid, второй — его row из
        # train user-items матрицы (для filter_already_liked_items).
        ids, _ = model._als.recommend(
            u_idx, train_user_items[u_idx],
            N=k, filter_already_liked_items=True,
        )
        rec_ids = [int(i) for i in ids]
        hits = len(set(rec_ids) & relevant)

        precisions.append(hits / k)
        recalls.append(hits / len(relevant))
        ndcgs.append(ndcg_at_k(rec_ids, relevant, k))

        if processed % _PROGRESS_EVERY == 0:
            elapsed = time.perf_counter() - t0
            rate = processed / elapsed if elapsed > 0 else 0
            eta = (len(in_model) - processed) / rate if rate > 0 else 0
            logger.info("[metrics-ALS] %d/%d юзеров (%.1f/с, ETA %.0f c)",
                        processed, len(in_model), rate, eta)

    result = {
        f"precision_at_{k}": float(np.mean(precisions)) if precisions else 0.0,
        f"recall_at_{k}": float(np.mean(recalls)) if recalls else 0.0,
        f"ndcg_at_{k}": float(np.mean(ndcgs)) if ndcgs else 0.0,
        "evaluated_users": int(len(precisions)),
        "k": k,
    }
    logger.info("[metrics-ALS] готово за %.2f c: P@%d=%.4f, R@%d=%.4f, NDCG@%d=%.4f",
                time.perf_counter() - total_start, k, result[f"precision_at_{k}"],
                k, result[f"recall_at_{k}"], k, result[f"ndcg_at_{k}"])
    return result


def make_ranking_chart_png(
    metrics: dict,
    title: str = "ALS — метрики ranking-качества",
) -> bytes:
    """Bar chart с Precision@K, Recall@K, NDCG@K. PNG bytes.

    Для implicit моделей где нет RMSE/MAE.
    """
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    k = metrics["k"]
    names = [f"Precision@{k}", f"Recall@{k}", f"NDCG@{k}"]
    vals = [
        metrics[f"precision_at_{k}"],
        metrics[f"recall_at_{k}"],
        metrics[f"ndcg_at_{k}"],
    ]

    fig, ax = plt.subplots(figsize=(9, 5))
    bars = ax.bar(names, vals, color=["#2ecc71", "#3498db", "#9b59b6"])
    ax.set_title(title, fontsize=13, fontweight="bold")
    ax.set_ylabel("Значение")
    ax.set_ylim(0, max(0.05, max(vals) * 1.4))
    for bar, v in zip(bars, vals):
        ax.text(bar.get_x() + bar.get_width() / 2, v, f"{v:.4f}",
                ha="center", va="bottom", fontsize=11)
    ax.grid(axis="y", linestyle=":", alpha=0.5)
    fig.tight_layout()

    buf = io.BytesIO()
    fig.savefig(buf, format="png", dpi=110, bbox_inches="tight")
    plt.close(fig)
    return buf.getvalue()


def make_chart_png(
    metrics: dict,
    title: str = "User-based Collaborative Filtering — метрики качества",
) -> bytes:
    """Рисует два bar chart (ошибки + ранжирование) и возвращает PNG bytes.

    Используем backend Agg — без GUI, безопасно в серверном процессе.
    title переопределяется для разных моделей (user-based / item-based).
    """
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    k = metrics["k"]
    p_key = f"precision_at_{k}"
    r_key = f"recall_at_{k}"

    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(11, 4.5))

    # Слева: ошибки предсказания оценок
    err_names = ["RMSE", "MAE"]
    err_vals = [metrics["rmse"], metrics["mae"]]
    bars1 = ax1.bar(err_names, err_vals, color=["#e74c3c", "#e67e22"])
    scale = metrics.get("rating_scale", 10)
    ax1.set_title("Ошибка предсказания оценки")
    ax1.set_ylabel(f"Значение (в шкале оценок 1–{scale})")
    ax1.set_ylim(0, max(err_vals) * 1.3 if max(err_vals) > 0 else 1)
    for bar, v in zip(bars1, err_vals):
        ax1.text(bar.get_x() + bar.get_width() / 2, v, f"{v:.3f}",
                 ha="center", va="bottom", fontsize=10)

    # Справа: качество топ-K рекомендаций
    rank_names = [f"Precision@{k}", f"Recall@{k}"]
    rank_vals = [metrics[p_key], metrics[r_key]]
    bars2 = ax2.bar(rank_names, rank_vals, color=["#2ecc71", "#3498db"])
    ax2.set_title(f"Качество топ-{k} рекомендаций")
    ax2.set_ylabel("Доля")
    ax2.set_ylim(0, max(0.05, max(rank_vals) * 1.4))
    for bar, v in zip(bars2, rank_vals):
        ax2.text(bar.get_x() + bar.get_width() / 2, v, f"{v:.4f}",
                 ha="center", va="bottom", fontsize=10)

    fig.suptitle(title, fontsize=13, fontweight="bold")
    fig.tight_layout()

    buf = io.BytesIO()
    fig.savefig(buf, format="png", dpi=110, bbox_inches="tight")
    plt.close(fig)
    return buf.getvalue()
