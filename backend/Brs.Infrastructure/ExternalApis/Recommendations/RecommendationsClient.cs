using System.Net;
using System.Net.Http.Json;
using Brs.Application.DTOs.Recommendations;

namespace Brs.Infrastructure.ExternalApis.Recommendations;

/// <summary>Состояние ответа Python-сервиса (для аккуратной обработки в сервисе).</summary>
internal enum PythonCallStatus { Ok, NotFound, NotTrained, Error }

/// <summary>Результат вызова Python-сервиса: статус + опционально payload.</summary>
internal record PythonResult<T>(PythonCallStatus Status, T? Value);

/// <summary>
/// Типизированный HTTP-клиент Python-сервиса рекомендаций.
/// Возвращает PythonResult, чтобы вызывающий код мог решить — fallback'нуться
/// или прокинуть ошибку дальше.
/// </summary>
public class RecommendationsClient
{
    private readonly HttpClient _http;

    public RecommendationsClient(HttpClient http) => _http = http;

    /// <summary>Сопоставляет нашу модель с URL-префиксом эндпоинта рекомендаций.</summary>
    private static string PrefixFor(RecommendationModel model) => model switch
    {
        RecommendationModel.Content => "recommendations",
        RecommendationModel.Svd => "svd/recommendations",
        RecommendationModel.Als => "als/recommendations",
        RecommendationModel.UserCf => "cf/recommendations",
        RecommendationModel.ItemCf => "item-cf/recommendations",
        _ => throw new InvalidOperationException($"Модель {model} не поддерживает per-user рекомендации")
    };

    /// <summary>Сопоставляет нашу модель с URL-префиксом эндпоинта переобучения (-local).</summary>
    private static string TrainPathFor(RecommendationModel model) => model switch
    {
        RecommendationModel.Content => "retrain",
        RecommendationModel.Svd => "svd/train-local",
        RecommendationModel.Als => "als/train-local",
        RecommendationModel.UserCf => "cf/train-local",
        RecommendationModel.ItemCf => "item-cf/train-local",
        _ => throw new InvalidOperationException($"Модель {model} не поддерживает retrain")
    };

    /// <summary>GET /{prefix}/{userId}?limit=N — персональные рекомендации.</summary>
    internal async Task<PythonResult<List<PythonRecommendationItem>>> RecommendForUserAsync(
        RecommendationModel model, Guid userId, int limit, CancellationToken ct)
    {
        var url = $"{PrefixFor(model)}/{userId}?limit={limit}";
        return await GetItemsAsync(url, isSimilar: false, ct);
    }

    /// <summary>GET /similar/{bookId} — content-based похожие книги.</summary>
    internal async Task<PythonResult<List<PythonRecommendationItem>>> ContentSimilarAsync(
        Guid bookId, int limit, CancellationToken ct)
        => await GetItemsAsync($"similar/{bookId}?limit={limit}", isSimilar: true, ct);

    /// <summary>GET /item-cf/similar/{bookId} — item-CF похожие («с этой книгой читают»).</summary>
    internal async Task<PythonResult<List<PythonRecommendationItem>>> ItemCfSimilarAsync(
        Guid bookId, int limit, CancellationToken ct)
        => await GetItemsAsync($"item-cf/similar/{bookId}?limit={limit}", isSimilar: true, ct);

    /// <summary>GET /health.</summary>
    internal async Task<PythonResult<PythonHealthResponse>> HealthAsync(CancellationToken ct)
    {
        try
        {
            var response = await _http.GetFromJsonAsync<PythonHealthResponse>("health", ct);
            return new(PythonCallStatus.Ok, response);
        }
        catch
        {
            return new(PythonCallStatus.Error, null);
        }
    }

    /// <summary>GET /{prefix}/metrics — метрики обученной модели.</summary>
    internal async Task<PythonResult<PythonMetricsResponse>> MetricsAsync(
        RecommendationModel model, CancellationToken ct)
    {
        // причина: content-based не считает метрик (это similarity-модель без train/test split)
        var path = model switch
        {
            RecommendationModel.Svd => "svd/metrics",
            RecommendationModel.Als => "als/metrics",
            RecommendationModel.UserCf => "cf/metrics",
            RecommendationModel.ItemCf => "item-cf/metrics",
            _ => null
        };
        if (path is null)
            return new(PythonCallStatus.NotFound, null);

        try
        {
            var response = await _http.GetAsync(path, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new(PythonCallStatus.NotFound, null);
            if (!response.IsSuccessStatusCode)
                return new(PythonCallStatus.Error, null);
            var metrics = await response.Content.ReadFromJsonAsync<PythonMetricsResponse>(cancellationToken: ct);
            return new(PythonCallStatus.Ok, metrics);
        }
        catch
        {
            return new(PythonCallStatus.Error, null);
        }
    }

    /// <summary>POST /{train path} — запускает обучение, возвращает true при успехе.</summary>
    internal async Task<bool> TrainAsync(RecommendationModel model, CancellationToken ct)
    {
        try
        {
            var response = await _http.PostAsync(TrainPathFor(model), content: null, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ---- общая обработка ответов ----

    private async Task<PythonResult<List<PythonRecommendationItem>>> GetItemsAsync(
        string url, bool isSimilar, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync(url, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new(PythonCallStatus.NotFound, null);
            // причина: 503 у Python означает «модель не обучена» — особый случай
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                return new(PythonCallStatus.NotTrained, null);
            if (!response.IsSuccessStatusCode)
                return new(PythonCallStatus.Error, null);

            // причина: эндпоинты /recommendations и /similar возвращают разный shape,
            // но оба содержат список items с полями book_id + similarity_score
            if (isSimilar)
            {
                var body = await response.Content.ReadFromJsonAsync<PythonSimilarResponse>(cancellationToken: ct);
                return new(PythonCallStatus.Ok, body?.Similar ?? new());
            }
            else
            {
                var body = await response.Content.ReadFromJsonAsync<PythonRecommendationsResponse>(cancellationToken: ct);
                return new(PythonCallStatus.Ok, body?.Recommendations ?? new());
            }
        }
        catch
        {
            // причина: таймауты/сетевые ошибки — сетевой Error, вызывающий код упадёт на fallback
            return new(PythonCallStatus.Error, null);
        }
    }
}
