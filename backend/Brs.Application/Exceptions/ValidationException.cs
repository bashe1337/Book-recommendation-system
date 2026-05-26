namespace Brs.Application.Exceptions;

/// <summary>Невалидные входные данные. Маппится на HTTP 400.</summary>
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
