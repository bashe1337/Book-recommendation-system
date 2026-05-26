using System.ComponentModel.DataAnnotations;

namespace Brs.Application.DTOs.Books;

/// <summary>Запрос создания новой книги.</summary>
public class CreateBookRequest
{
    [Required, MinLength(1), MaxLength(512)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
    public int? PublishedYear { get; set; }
    public string? Language { get; set; }
    public string? ISBN { get; set; }
    public string? CoverUrl { get; set; }
    public string? GoogleBooksId { get; set; }

    public List<Guid> AuthorIds { get; set; } = new();
    public List<Guid> GenreIds { get; set; } = new();
    public List<Guid> TagIds { get; set; } = new();
}
