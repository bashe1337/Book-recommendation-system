using System.ComponentModel.DataAnnotations;

namespace Brs.Application.DTOs.Books;

/// <summary>Запрос создания редакционной подборки.</summary>
public class CreateEditorialListRequest
{
    [Required, MinLength(1), MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<Guid> BookIds { get; set; } = new();
}
