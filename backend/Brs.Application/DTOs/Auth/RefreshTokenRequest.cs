using System.ComponentModel.DataAnnotations;

namespace Brs.Application.DTOs.Auth;

/// <summary>Запрос обновления access-токена по refresh-токену.</summary>
public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
