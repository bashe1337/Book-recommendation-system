namespace Brs.Application.DTOs.Books;

/// <summary>
/// Облегчённое представление книги для списков и каруселей —
/// причина: не грузит описание, теги и полные объекты авторов/жанров.
/// </summary>
public class BookSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public double AvgRating { get; set; }
    public int RatingCount { get; set; }

    public List<string> Authors { get; set; } = new();
    public List<string> Genres { get; set; } = new();
}
