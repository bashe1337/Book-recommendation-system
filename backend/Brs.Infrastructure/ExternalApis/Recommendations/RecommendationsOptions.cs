namespace Brs.Infrastructure.ExternalApis.Recommendations;

/// <summary>Опции интеграции с Python-сервисом рекомендаций.</summary>
public class RecommendationsOptions
{
    public const string SectionName = "Recommendations";

    public string BaseUrl { get; set; } = "http://localhost:8001";

    /// <summary>Таймаут запроса к ML-сервису (секунды).</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Сколько оценок нужно пользователю, чтобы переключиться с content-based на SVD.</summary>
    public int SvdThresholdRatings { get; set; } = 10;

    /// <summary>Период фонового переобучения моделей (часы).</summary>
    public int RetrainIntervalHours { get; set; } = 24;

    /// <summary>На сколько часов кэшируется «рекомендация дня».</summary>
    public int DailyCacheHours { get; set; } = 24;
}
