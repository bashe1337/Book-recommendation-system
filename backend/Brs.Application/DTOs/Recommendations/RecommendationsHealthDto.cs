namespace Brs.Application.DTOs.Recommendations;

/// <summary>Состояние ML-сервиса и обученности каждой модели.</summary>
public class RecommendationsHealthDto
{
    public bool ServiceAvailable { get; set; }

    public Dictionary<string, ModelHealth> Models { get; set; } = new();
}

/// <summary>Статус одной модели.</summary>
public class ModelHealth
{
    public bool Fitted { get; set; }
    public int? BookCount { get; set; }

    /// <summary>Имя последней использованной метрики (для отображения), может быть null.</summary>
    public string? Metric { get; set; }
    public double? MetricValue { get; set; }
}
