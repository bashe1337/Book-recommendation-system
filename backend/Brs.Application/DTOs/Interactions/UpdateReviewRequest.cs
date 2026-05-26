using System.ComponentModel.DataAnnotations;

namespace Brs.Application.DTOs.Interactions;

/// <summary>Запрос редактирования отзыва.</summary>
public class UpdateReviewRequest
{
    [Required, MinLength(1), MaxLength(5000)]
    public string Content { get; set; } = string.Empty;
}
