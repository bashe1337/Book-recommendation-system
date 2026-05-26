namespace Brs.Application.DTOs.Interactions;

/// <summary>Отзыв пользователя о книге.</summary>
public class ReviewDto
{
    public Guid Id { get; set; }
    public Guid BookId { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // причина: тональность вычисляется ML-сервисом асинхронно, может быть null
    public float? SentimentScore { get; set; }
}
