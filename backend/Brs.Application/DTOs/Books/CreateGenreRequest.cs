using System.ComponentModel.DataAnnotations;

namespace Brs.Application.DTOs.Books;

/// <summary>Запрос создания жанра.</summary>
public class CreateGenreRequest
{
    [Required, MinLength(1), MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [Required, MinLength(1), MaxLength(128)]
    public string Slug { get; set; } = string.Empty;
}
