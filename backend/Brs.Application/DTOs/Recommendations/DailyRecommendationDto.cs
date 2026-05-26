using Brs.Application.DTOs.Books;

namespace Brs.Application.DTOs.Recommendations;

/// <summary>«Рекомендация дня» — одна книга + информация о модели.</summary>
public class DailyRecommendationDto
{
    public BookSummaryDto Book { get; set; } = new();

    /// <summary>Модель с лучшей метрикой на момент выбора.</summary>
    public RecommendationModel Model { get; set; }

    /// <summary>Дата, на которую сформирована рекомендация (UTC, начало суток).</summary>
    public DateTime Date { get; set; }

    public RecommendationSource Source { get; set; }
}
