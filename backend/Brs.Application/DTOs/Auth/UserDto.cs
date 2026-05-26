namespace Brs.Application.DTOs.Auth;

/// <summary>Публичное представление пользователя (без пароля и токенов).</summary>
public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public List<Guid> PreferredGenreIds { get; set; } = new();
}
