namespace Brs.Application.DTOs.Auth;

/// <summary>Ответ с парой токенов и данными пользователя.</summary>
public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;

    // причина: фронту удобно знать абсолютное время истечения, чтобы
    // запланировать рефреш без пересчёта от now()
    public DateTime ExpiresAt { get; set; }

    public UserDto User { get; set; } = new();
}
