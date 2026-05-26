namespace Brs.Application.DTOs.Books;

/// <summary>Параметры фильтрации, сортировки и пагинации каталога книг.</summary>
public class BookFilterRequest
{
    public string? Search { get; set; }
    public List<Guid>? GenreIds { get; set; }
    public Guid? AuthorId { get; set; }
    public string? Language { get; set; }
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }

    /// <summary>Поле сортировки: "rating" | "date" | "popularity". По умолчанию "rating".</summary>
    public string? SortBy { get; set; } = "rating";

    public bool SortDesc { get; set; } = true;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
