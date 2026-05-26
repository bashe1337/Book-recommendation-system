namespace Brs.Application.DTOs.Books;

/// <summary>
/// Запрос обновления книги. Все поля nullable —
/// причина: частичное обновление, переданы только изменяемые значения.
/// </summary>
public class UpdateBookRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int? PublishedYear { get; set; }
    public string? Language { get; set; }
    public string? ISBN { get; set; }
    public string? CoverUrl { get; set; }
    public string? GoogleBooksId { get; set; }

    public List<Guid>? AuthorIds { get; set; }
    public List<Guid>? GenreIds { get; set; }
    public List<Guid>? TagIds { get; set; }
}
