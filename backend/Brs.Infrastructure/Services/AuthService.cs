using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Brs.Application.DTOs.Auth;
using Brs.Application.Exceptions;
using Brs.Application.Interfaces;
using Brs.Domain.Entities;
using Brs.Infrastructure.Auth;
using Brs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Brs.Infrastructure.Services;

/// <summary>Сервис регистрации/входа и управления токенами.</summary>
public class AuthService : IAuthService
{
    private readonly BrsDbContext _db;
    private readonly JwtOptions _jwt;

    public AuthService(BrsDbContext db, IOptions<JwtOptions> jwt)
    {
        _db = db;
        _jwt = jwt.Value;
    }

    /// <inheritdoc/>
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == emailNormalized, ct))
            throw new ValidationException($"Email '{request.Email}' уже занят.");

        if (await _db.Users.AnyAsync(u => u.Username == request.Username, ct))
            throw new ValidationException($"Имя пользователя '{request.Username}' уже занято.");

        var user = new User
        {
            Email = emailNormalized,
            // причина: BCrypt автоматически генерирует salt и встраивает его в хеш
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Username = request.Username,
            Role = UserRole.User,
            RegisteredAt = DateTime.UtcNow
        };

        await _db.Users.AddAsync(user, ct);
        await IssueTokensAsync(user, ct);
        return BuildResponse(user);
    }

    /// <inheritdoc/>
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var emailNormalized = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == emailNormalized, ct)
            ?? throw new UnauthorizedException("Неверный email или пароль.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            // причина: одинаковое сообщение для отсутствующего пользователя и неверного
            // пароля — мера против перебора (не раскрываем, существует ли email)
            throw new UnauthorizedException("Неверный email или пароль.");

        await IssueTokensAsync(user, ct);
        return BuildResponse(user);
    }

    /// <inheritdoc/>
    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken, ct)
            ?? throw new UnauthorizedException("Refresh token недействителен.");

        if (user.RefreshTokenExpiresAt is null || user.RefreshTokenExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedException("Refresh token истёк.");

        await IssueTokensAsync(user, ct);
        return BuildResponse(user);
    }

    /// <inheritdoc/>
    public async Task RevokeTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync(new object[] { userId }, ct)
            ?? throw new NotFoundException($"Пользователь {userId} не найден.");

        user.RefreshToken = null;
        user.RefreshTokenExpiresAt = null;
        await _db.SaveChangesAsync(ct);
    }

    // ---- helpers ----

    /// <summary>Генерирует новую пару токенов и сохраняет refresh в БД.</summary>
    private async Task IssueTokensAsync(User user, CancellationToken ct)
    {
        user.RefreshToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Собирает AuthResponse: свежий JWT + текущий refresh.</summary>
    private AuthResponse BuildResponse(User user)
    {
        var (access, expiresAt) = GenerateJwt(user);
        return new AuthResponse
        {
            AccessToken = access,
            RefreshToken = user.RefreshToken!,
            ExpiresAt = expiresAt,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                Role = user.Role.ToString(),
                PreferredGenreIds = user.PreferredGenreIds
            }
        };
    }

    /// <summary>Создаёт подписанный JWT с базовыми claims.</summary>
    private (string token, DateTime expiresAt) GenerateJwt(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            // причина: sub содержит userId — общепринятый стандарт; парсим обратно в контроллерах
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            // причина: jti уникален для каждого токена — позволяет в будущем
            // вести чёрный список отозванных access-токенов
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpiryMinutes);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
