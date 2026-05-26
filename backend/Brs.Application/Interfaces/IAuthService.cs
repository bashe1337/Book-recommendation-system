using Brs.Application.DTOs.Auth;

namespace Brs.Application.Interfaces;

/// <summary>Сценарии регистрации, входа и работы с токенами.</summary>
public interface IAuthService
{
    /// <summary>Регистрирует нового пользователя и возвращает пару токенов.</summary>
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>Проверяет учётные данные и возвращает пару токенов.</summary>
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>Обновляет access-токен по действующему refresh-токену.</summary>
    Task<AuthResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Отзывает refresh-токен пользователя (сессия завершена).</summary>
    Task RevokeTokenAsync(Guid userId, CancellationToken ct = default);
}
