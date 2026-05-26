namespace Brs.Application.DTOs.Recommendations;

/// <summary>Источник рекомендаций — реальная ML-модель или fallback.</summary>
public enum RecommendationSource
{
    /// <summary>Ответ от ML-сервиса (модель обучена, пользователь/книга в выборке).</summary>
    Ml = 0,
    /// <summary>Подмена популярным/жанровым списком (модель не готова или не нашла данных).</summary>
    Fallback = 1
}
