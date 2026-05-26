namespace Brs.Application.DTOs.Books;

/// <summary>Универсальная обёртка постраничного результата.</summary>
/// <typeparam name="T">Тип элемента страницы.</typeparam>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }

    /// <summary>Всего страниц при текущем размере страницы.</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
}
