namespace Brs.Application.Exceptions;

/// <summary>Сущность не найдена в БД. Маппится на HTTP 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
