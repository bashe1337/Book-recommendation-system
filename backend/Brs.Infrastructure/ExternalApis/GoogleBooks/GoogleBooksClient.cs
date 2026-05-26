using System.Net.Http.Json;
using Brs.Application.DTOs.Books;
using Microsoft.Extensions.Options;

namespace Brs.Infrastructure.ExternalApis.GoogleBooks;

/// <summary>Типизированный HTTP-клиент для Google Books API.</summary>
public class GoogleBooksClient
{
    private readonly HttpClient _http;
    private readonly GoogleBooksOptions _options;

    public GoogleBooksClient(HttpClient http, IOptions<GoogleBooksOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    /// <summary>
    /// Ищет тома по запросу с учётом области поиска и ограничения языка.
    /// Возвращает пустой список при отсутствии результатов.
    /// </summary>
    /// <param name="query">Поисковая строка пользователя.</param>
    /// <param name="field">Область поиска: все поля, название, автор или ISBN.</param>
    /// <param name="langRestrict">Двухбуквенный код языка (например "ru") или null.</param>
    /// <param name="maxResults">Максимум результатов (Google ограничивает 40).</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task<List<GoogleBooksVolume>> SearchAsync(
        string query,
        GoogleBooksSearchField field = GoogleBooksSearchField.All,
        string? langRestrict = null,
        int maxResults = 10,
        CancellationToken ct = default)
    {
        var expression = BuildQueryExpression(query, field);

        // причина: экранируем готовое выражение целиком — двоеточие спец-ключа (%3A)
        // Google корректно декодирует обратно в "intitle:" / "inauthor:" / "isbn:"
        var url = $"volumes?q={Uri.EscapeDataString(expression)}&maxResults={maxResults}";

        if (!string.IsNullOrWhiteSpace(langRestrict))
            // причина: langRestrict сужает выдачу к изданиям на нужном языке (например "ru")
            url += $"&langRestrict={Uri.EscapeDataString(langRestrict.Trim())}";

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            url += $"&key={_options.ApiKey}";

        var response = await _http.GetFromJsonAsync<GoogleBooksResponse>(url, ct);
        return response?.Items ?? new List<GoogleBooksVolume>();
    }

    /// <summary>
    /// Формирует выражение для параметра q с учётом спец-ключей Google Books.
    /// причина: intitle: ограничивает поиск названием, inauthor: — автором,
    /// isbn: — конкретным идентификатором издания.
    /// </summary>
    private static string BuildQueryExpression(string query, GoogleBooksSearchField field)
    {
        var trimmed = query.Trim();
        return field switch
        {
            GoogleBooksSearchField.Title => $"intitle:{trimmed}",
            GoogleBooksSearchField.Author => $"inauthor:{trimmed}",
            // причина: ISBN нормализуем — Google ждёт чистые цифры/X без дефисов и пробелов
            GoogleBooksSearchField.Isbn => $"isbn:{NormalizeIsbn(trimmed)}",
            _ => trimmed
        };
    }

    /// <summary>Убирает из ISBN все символы, кроме цифр и финальной X (ISBN-10).</summary>
    private static string NormalizeIsbn(string isbn)
    {
        Span<char> buffer = stackalloc char[isbn.Length];
        var written = 0;
        foreach (var ch in isbn)
        {
            if (char.IsDigit(ch) || ch == 'X' || ch == 'x')
                buffer[written++] = char.ToUpperInvariant(ch);
        }
        return new string(buffer[..written]);
    }
}
