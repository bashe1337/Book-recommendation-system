namespace Brs.Domain.Entities;

/// <summary>Текстовый отзыв пользователя о книге.</summary>
public class Review : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid BookId { get; set; }

    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // причина: оценка тональности (-1..1) вычисляется фоновым ML-сервисом
    // и может отсутствовать у только что созданных отзывов
    public float? SentimentScore { get; set; }

    public User? User { get; set; }
    public Book? Book { get; set; }
}
