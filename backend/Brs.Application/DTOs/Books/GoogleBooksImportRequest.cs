using System.ComponentModel.DataAnnotations;

namespace Brs.Application.DTOs.Books;

/// <summary>Запрос импорта книг из Google Books API по поисковой строке.</summary>
public class GoogleBooksImportRequest
{
    [Required, MinLength(1)]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Область поиска: All / Title / Author / Isbn.
    /// причина: для Isbn остальные параметры (автор, название) не нужны — поиск точечный
    /// по идентификатору издания.
    /// </summary>
    public GoogleBooksSearchField SearchField { get; set; } = GoogleBooksSearchField.All;

    /// <summary>
    /// Ограничение языка результатов (двухбуквенный код, например "ru").
    /// причина: соответствует параметру langRestrict в Google Books API —
    /// возвращает только издания на указанном языке.
    /// </summary>
    public string? LangRestrict { get; set; }
}
