namespace Brs.Application.DTOs.Interactions;

/// <summary>Агрегированная статистика взаимодействий с книгой.</summary>
public class BookStatsDto
{
    public Guid BookId { get; set; }
    public float AvgRating { get; set; }
    public int RatingCount { get; set; }
    public int ReviewCount { get; set; }
    public int FavoriteCount { get; set; }
}
