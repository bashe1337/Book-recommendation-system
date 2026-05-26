using Brs.Application.DTOs.Auth;
using Brs.Application.DTOs.Users;
using Brs.Application.Exceptions;
using Brs.Application.Interfaces;
using Brs.Infrastructure.Persistence;

namespace Brs.Infrastructure.Services;

/// <summary>Сервис работы с профилем пользователя.</summary>
public class UserService : IUserService
{
    private readonly BrsDbContext _db;

    public UserService(BrsDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException($"Пользователь {id} не найден.");

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            Role = user.Role.ToString(),
            PreferredGenreIds = user.PreferredGenreIds
        };
    }

    /// <inheritdoc/>
    public async Task<UserDto> UpdateProfileAsync(Guid id, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException($"Пользователь {id} не найден.");

        user.Username = request.Username;
        user.PreferredGenreIds = request.PreferredGenreIds ?? new List<Guid>();
        await _db.SaveChangesAsync(ct);

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            Role = user.Role.ToString(),
            PreferredGenreIds = user.PreferredGenreIds
        };
    }
}
