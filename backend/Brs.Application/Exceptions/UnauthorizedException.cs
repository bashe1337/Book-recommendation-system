namespace Brs.Application.Exceptions;

/// <summary>Ошибка аутентификации/авторизации. Маппится на HTTP 401.</summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}
