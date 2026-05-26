namespace Brs.Domain.Entities;

/// <summary>Книга, добавленная пользователем в избранное.</summary>
public class Favorite : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid BookId { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Book? Book { get; set; }
}
