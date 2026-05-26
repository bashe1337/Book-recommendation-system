using Brs.Application.DTOs.Auth;
using Brs.Application.DTOs.Users;

namespace Brs.Application.Interfaces;

/// <summary>Операции над профилем пользователя.</summary>
public interface IUserService
{
    /// <summary>Возвращает пользователя по идентификатору.</summary>
    Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Обновляет профиль (username и предпочитаемые жанры).</summary>
    Task<UserDto> UpdateProfileAsync(Guid id, UpdateProfileRequest request, CancellationToken ct = default);
}
