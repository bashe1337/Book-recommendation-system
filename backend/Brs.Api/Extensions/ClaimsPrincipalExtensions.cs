using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Brs.Application.Exceptions;

namespace Brs.Api.Extensions;

/// <summary>Утилиты извлечения данных текущего пользователя из JWT claims.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>Возвращает идентификатор текущего пользователя из claims.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        // причина: JwtBearer по умолчанию маппит "sub" в ClaimTypes.NameIdentifier,
        // но проверяем оба варианта на случай отключённого маппинга
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new UnauthorizedException("Токен не содержит идентификатор пользователя.");
        return Guid.Parse(id);
    }

    /// <summary>Проверяет, обладает ли текущий пользователь ролью администратора.</summary>
    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole("Admin");
}
