using System.ComponentModel.DataAnnotations;

namespace Brs.Application.DTOs.Auth;

/// <summary>Запрос регистрации нового пользователя.</summary>
public class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [Required, MinLength(1), MaxLength(64)]
    public string Username { get; set; } = string.Empty;
}
