using Brs.Application.DTOs.Books;

namespace Brs.Application.DTOs.Recommendations;

/// <summary>Унифицированный ответ блока рекомендаций для фронта.</summary>
public class RecommendationListDto
{
    public List<BookSummaryDto> Items { get; set; } = new();

    /// <summary>Алгоритм, давший этот ответ (для подписи под блоком на фронте).</summary>
    public RecommendationModel Model { get; set; }

    /// <summary>ML или fallback (фронт может показать «персонализировано» / «популярное»).</summary>
    public RecommendationSource Source { get; set; }

    /// <summary>Краткое объяснение, как составилась подборка (для фронта).</summary>
    public string Explanation { get; set; } = string.Empty;
}
