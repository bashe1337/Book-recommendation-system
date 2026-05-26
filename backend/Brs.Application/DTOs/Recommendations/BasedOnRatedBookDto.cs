using Brs.Application.DTOs.Books;

namespace Brs.Application.DTOs.Recommendations;

/// <summary>
/// Группа рекомендаций «похожее на оценённую книгу».
/// причина: фронт рендерит блок «Похожее на <i>X</i>» с подписью названия источника.
/// </summary>
public class BasedOnRatedBookDto
{
    /// <summary>Книга-источник (одна из оценённых пользователем).</summary>
    public BookSummaryDto Source { get; set; } = new();

    /// <summary>Алгоритм, который собрал список похожих.</summary>
    public RecommendationModel Model { get; set; }

    /// <summary>Список похожих книг.</summary>
    public List<BookSummaryDto> Items { get; set; } = new();
}
