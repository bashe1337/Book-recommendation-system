namespace Brs.Application.DTOs.Books;

/// <summary>Полное представление книги со всеми связями.</summary>
public class BookDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? PublishedYear { get; set; }
    public string? Language { get; set; }
    public string? ISBN { get; set; }
    public string? CoverUrl { get; set; }
    public double AvgRating { get; set; }
    public int RatingCount { get; set; }

    public List<AuthorDto> Authors { get; set; } = new();
    public List<GenreDto> Genres { get; set; } = new();
    public List<TagDto> Tags { get; set; } = new();
}
