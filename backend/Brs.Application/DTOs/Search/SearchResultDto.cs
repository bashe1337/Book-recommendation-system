using Brs.Application.DTOs.Books;

namespace Brs.Application.DTOs.Search;

/// <summary>Результат поиска с метаданными пагинации и временем выполнения.</summary>
public class SearchResultDto
{
    public List<BookSummaryDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

    /// <summary>Оригинальный поисковый запрос (для подсветки на фронте).</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>Время выполнения поиска в миллисекундах.</summary>
    public long SearchTimeMs { get; set; }
}
