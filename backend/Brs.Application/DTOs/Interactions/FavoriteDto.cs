using Brs.Application.DTOs.Books;

namespace Brs.Application.DTOs.Interactions;

/// <summary>Книга в избранном с краткой карточкой.</summary>
public class FavoriteDto
{
    public Guid Id { get; set; }
    public Guid BookId { get; set; }
    public DateTime AddedAt { get; set; }
    public BookSummaryDto Book { get; set; } = new();
}
