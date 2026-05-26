namespace Brs.Application.DTOs.Books;

/// <summary>Представление редакционной подборки с краткими карточками книг.</summary>
public class EditorialListDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }

    public List<BookSummaryDto> Books { get; set; } = new();
}
