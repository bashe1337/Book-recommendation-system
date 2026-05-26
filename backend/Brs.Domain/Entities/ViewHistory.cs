namespace Brs.Domain.Entities;

/// <summary>Запись просмотра карточки книги пользователем.</summary>
public class ViewHistory : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid BookId { get; set; }

    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Book? Book { get; set; }
}
