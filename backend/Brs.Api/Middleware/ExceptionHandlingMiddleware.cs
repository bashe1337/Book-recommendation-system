using System.Text.Json;
using Brs.Application.Exceptions;

namespace Brs.Api.Middleware;

/// <summary>
/// Глобальный перехватчик исключений. Маппит кастомные исключения
/// на HTTP-коды и возвращает единый JSON-формат ошибки.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Запускает следующий компонент конвейера и обрабатывает выброшенные исключения.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        // причина: маппим типы исключений в коды HTTP — фронт ориентируется по статусу
        var (status, message) = ex switch
        {
            NotFoundException nf => (StatusCodes.Status404NotFound, nf.Message),
            ValidationException ve => (StatusCodes.Status400BadRequest, ve.Message),
            UnauthorizedException ue => (StatusCodes.Status401Unauthorized, ue.Message),
            _ => (StatusCodes.Status500InternalServerError, "Внутренняя ошибка сервера.")
        };

        if (status >= 500)
            _logger.LogError(ex, "Необработанное исключение");
        else
            _logger.LogWarning(ex, "Обработанное исключение: {Message}", ex.Message);

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new { error = message, statusCode = status },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await context.Response.WriteAsync(body);
    }
}
