using System.Text.Json.Serialization;

namespace Brs.Infrastructure.ExternalApis.Recommendations;

/// <summary>POCO-обёртки для парсинга JSON-ответов Python-сервиса.</summary>
internal class PythonRecommendationItem
{
    [JsonPropertyName("book_id")]
    public string BookId { get; set; } = string.Empty;

    [JsonPropertyName("similarity_score")]
    public double? SimilarityScore { get; set; }

    [JsonPropertyName("predicted_rating")]
    public double? PredictedRating { get; set; }

    /// <summary>Единая оценка релевантности (берём first non-null).</summary>
    public double Score => SimilarityScore ?? PredictedRating ?? 0d;
}

internal class PythonRecommendationsResponse
{
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("recommendations")]
    public List<PythonRecommendationItem> Recommendations { get; set; } = new();
}

internal class PythonSimilarResponse
{
    [JsonPropertyName("book_id")]
    public string? BookId { get; set; }

    [JsonPropertyName("similar")]
    public List<PythonRecommendationItem> Similar { get; set; } = new();
}

internal class PythonHealthResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("model_fitted")]
    public bool ModelFitted { get; set; }

    [JsonPropertyName("book_count")]
    public int BookCount { get; set; }
}

internal class PythonMetricsResponse
{
    [JsonPropertyName("k")]
    public int? K { get; set; }

    [JsonPropertyName("rmse")]
    public double? Rmse { get; set; }

    [JsonPropertyName("mae")]
    public double? Mae { get; set; }

    [JsonPropertyName("precision_at_10")]
    public double? PrecisionAt10 { get; set; }

    [JsonPropertyName("recall_at_10")]
    public double? RecallAt10 { get; set; }

    [JsonPropertyName("ndcg_at_10")]
    public double? NdcgAt10 { get; set; }
}
