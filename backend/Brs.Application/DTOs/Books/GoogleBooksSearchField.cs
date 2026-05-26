namespace Brs.Application.DTOs.Books;

/// <summary>
/// Область поиска в Google Books — определяет, какой спец-ключ
/// (intitle:/inauthor:) подставляется в параметр q запроса.
/// </summary>
public enum GoogleBooksSearchField
{
    /// <summary>Поиск по всем полям (заголовок, автор, описание и т. д.).</summary>
    All = 0,

    /// <summary>Только по названию книги (Google: intitle:).</summary>
    Title = 1,

    /// <summary>По автору / книги конкретного автора (Google: inauthor:).</summary>
    Author = 2,

    /// <summary>Только по ISBN — точный поиск конкретного издания (Google: isbn:).</summary>
    Isbn = 3
}
