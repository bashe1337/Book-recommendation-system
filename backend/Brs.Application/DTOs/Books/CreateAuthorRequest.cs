using System.ComponentModel.DataAnnotations;

namespace Brs.Application.DTOs.Books;

/// <summary>Запрос создания автора.</summary>
public class CreateAuthorRequest
{
    [Required, MinLength(1), MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    public string? Bio { get; set; }
}
