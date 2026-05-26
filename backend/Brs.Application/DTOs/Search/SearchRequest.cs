namespace Brs.Application.DTOs.Search;

/// <summary>Параметры расширенного поиска книг.</summary>
public class SearchRequest
{
    public string? Query { get; set; }
    public List<Guid>? GenreIds { get; set; }
    public Guid? AuthorId { get; set; }
    public string? Language { get; set; }
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }

    /// <summary>Минимальный средний рейтинг книги.</summary>
    public float? MinRating { get; set; }

    /// <summary>Сортировка: "relevance" | "rating" | "date" | "popularity". По умолчанию relevance.</summary>
    public string? SortBy { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Есть ли хотя бы один фильтр (помимо текста запроса).</summary>
    public bool HasAnyFilter =>
        (GenreIds is { Count: > 0 })
        || AuthorId.HasValue
        || !string.IsNullOrWhiteSpace(Language)
        || YearFrom.HasValue
        || YearTo.HasValue
        || MinRating.HasValue;
}
