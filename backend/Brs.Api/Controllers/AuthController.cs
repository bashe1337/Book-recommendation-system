using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Brs.Application.DTOs.Auth;
using Brs.Application.Exceptions;
using Brs.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brs.Api.Controllers;

/// <summary>Аутентификация: регистрация, вход, обновление и отзыв токенов.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>POST /api/auth/register — регистрирует нового пользователя.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var response = await _auth.RegisterAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>POST /api/auth/login — проверяет учётные данные и возвращает пару токенов.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
        => Ok(await _auth.LoginAsync(request, ct));

    /// <summary>POST /api/auth/refresh — выдаёт новую пару токенов по refresh-токену.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
        => Ok(await _auth.RefreshTokenAsync(request.RefreshToken, ct));

    /// <summary>POST /api/auth/revoke — отзывает refresh-токен текущего пользователя.</summary>
    [HttpPost("revoke")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(CancellationToken ct)
    {
        // причина: вытаскиваем userId из sub-claim — единый источник идентичности
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedException("Токен не содержит идентификатор пользователя.");

        await _auth.RevokeTokenAsync(Guid.Parse(sub), ct);
        return NoContent();
    }
}
