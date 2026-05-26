using System.ComponentModel.DataAnnotations;

namespace Brs.Application.DTOs.Interactions;

/// <summary>Запрос создания отзыва.</summary>
public class CreateReviewRequest
{
    [Required, MinLength(1), MaxLength(5000)]
    public string Content { get; set; } = string.Empty;
}
