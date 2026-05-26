namespace Brs.Infrastructure.ExternalApis.GoogleBooks;

/// <summary>Опции клиента Google Books, биндятся из секции "GoogleBooks".</summary>
public class GoogleBooksOptions
{
    public const string SectionName = "GoogleBooks";

    public string BaseUrl { get; set; } = "https://www.googleapis.com/books/v1";

    // причина: ключ необязателен — без него API работает с пониженным лимитом
    public string ApiKey { get; set; } = string.Empty;
}
