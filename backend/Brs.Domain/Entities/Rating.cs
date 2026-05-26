namespace Brs.Domain.Entities;

/// <summary>Числовая оценка книги пользователем по шкале 1–5.</summary>
public class Rating : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid BookId { get; set; }

    // причина: ограничение диапазона проверяется как на уровне приложения,
    // так и на уровне БД через CHECK-индекс (см. конфигурацию)
    public int Score { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Book? Book { get; set; }
}
