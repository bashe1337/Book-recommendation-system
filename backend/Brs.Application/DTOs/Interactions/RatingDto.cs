namespace Brs.Application.DTOs.Interactions;

/// <summary>Оценка книги пользователем.</summary>
public class RatingDto
{
    public Guid Id { get; set; }
    public Guid BookId { get; set; }
    public Guid UserId { get; set; }
    public int Score { get; set; }
    public DateTime CreatedAt { get; set; }
}
