using System.ComponentModel.DataAnnotations;

namespace Brs.Application.DTOs.Users;

/// <summary>Запрос обновления профиля пользователя.</summary>
public class UpdateProfileRequest
{
    [Required, MinLength(1), MaxLength(64)]
    public string Username { get; set; } = string.Empty;

    public List<Guid> PreferredGenreIds { get; set; } = new();
}
