namespace Brs.Application.DTOs.Recommendations;

/// <summary>Ответ блока «Похожее на оценённые книги» — несколько групп.</summary>
public class BasedOnMyRatingsResponseDto
{
    public List<BasedOnRatedBookDto> Groups { get; set; } = new();

    /// <summary>Источник: ML если хотя бы одна группа реальная, иначе fallback.</summary>
    public RecommendationSource Source { get; set; }
}
