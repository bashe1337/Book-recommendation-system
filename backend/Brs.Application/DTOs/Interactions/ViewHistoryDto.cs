using Brs.Application.DTOs.Books;

namespace Brs.Application.DTOs.Interactions;

/// <summary>Запись истории просмотров с краткой карточкой книги.</summary>
public class ViewHistoryDto
{
    public Guid Id { get; set; }
    public Guid BookId { get; set; }
    public DateTime ViewedAt { get; set; }
    public BookSummaryDto Book { get; set; } = new();
}
