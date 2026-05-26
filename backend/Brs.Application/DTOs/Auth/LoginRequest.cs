using System.ComponentModel.DataAnnotations;

namespace Brs.Application.DTOs.Auth;

/// <summary>Запрос входа в систему по email и паролю.</summary>
public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
