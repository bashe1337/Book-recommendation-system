using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Brs.Application.DTOs.Auth;
using Brs.Application.DTOs.Users;
using Brs.Application.Exceptions;
using Brs.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brs.Api.Controllers;

/// <summary>Управление профилем текущего пользователя.</summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users) => _users = users;

    /// <summary>GET /api/users/me — возвращает профиль текущего пользователя.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
        => Ok(await _users.GetByIdAsync(GetCurrentUserId(), ct));

    /// <summary>PUT /api/users/me — обновляет username и предпочитаемые жанры.</summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> UpdateMe(
        [FromBody] UpdateProfileRequest request, CancellationToken ct)
        => Ok(await _users.UpdateProfileAsync(GetCurrentUserId(), request, ct));

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException("Токен не содержит идентификатор пользователя.");
        return Guid.Parse(sub);
    }
}
