using Brs.Application.DTOs.Books;

namespace Brs.Application.Interfaces;

/// <summary>Сценарии работы с каталогом: книги, авторы, жанры, подборки, импорт.</summary>
public interface ICatalogService
{
    /// <summary>Возвращает страницу книг по фильтру с сортировкой и пагинацией.</summary>
    Task<PagedResult<BookSummaryDto>> GetBooksAsync(BookFilterRequest filter, CancellationToken ct = default);

    /// <summary>Возвращает книгу со всеми связями или бросает NotFoundException.</summary>
    Task<BookDto> GetBookByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Создаёт новую книгу и привязывает авторов/жанры/теги.</summary>
    Task<BookDto> CreateBookAsync(CreateBookRequest request, CancellationToken ct = default);

    /// <summary>Частично обновляет книгу.</summary>
    Task<BookDto> UpdateBookAsync(Guid id, UpdateBookRequest request, CancellationToken ct = default);

    /// <summary>Удаляет книгу.</summary>
    Task DeleteBookAsync(Guid id, CancellationToken ct = default);

    /// <summary>Возвращает всех авторов.</summary>
    Task<List<AuthorDto>> GetAuthorsAsync(CancellationToken ct = default);

    /// <summary>Создаёт автора.</summary>
    Task<AuthorDto> CreateAuthorAsync(CreateAuthorRequest request, CancellationToken ct = default);

    /// <summary>Возвращает все жанры.</summary>
    Task<List<GenreDto>> GetGenresAsync(CancellationToken ct = default);

    /// <summary>Создаёт жанр.</summary>
    Task<GenreDto> CreateGenreAsync(CreateGenreRequest request, CancellationToken ct = default);

    /// <summary>Возвращает активные редакционные подборки.</summary>
    Task<List<EditorialListDto>> GetEditorialListsAsync(CancellationToken ct = default);

    /// <summary>Создаёт редакционную подборку из указанных книг.</summary>
    Task<EditorialListDto> CreateEditorialListAsync(string title, string description, List<Guid> bookIds, CancellationToken ct = default);

    /// <summary>Импортирует книги из Google Books API с учётом области поиска и языка.</summary>
    Task<List<BookSummaryDto>> ImportFromGoogleBooksAsync(
        string query,
        GoogleBooksSearchField field = GoogleBooksSearchField.All,
        string? langRestrict = null,
        CancellationToken ct = default);
}
