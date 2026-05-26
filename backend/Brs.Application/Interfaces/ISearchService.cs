using Brs.Application.DTOs.Books;
using Brs.Application.DTOs.Search;

namespace Brs.Application.Interfaces;

/// <summary>Сценарии расширенного поиска: полнотекстовый, нечёткий, автодополнение, похожие.</summary>
public interface ISearchService
{
    /// <summary>Выполняет поиск книг с фильтрами, сортировкой и пагинацией.</summary>
    Task<SearchResultDto> SearchAsync(SearchRequest request, CancellationToken ct = default);

    /// <summary>Возвращает подсказки автодополнения по книгам, авторам и жанрам.</summary>
    Task<AutocompleteResultDto> AutocompleteAsync(AutocompleteRequest request, CancellationToken ct = default);

    /// <summary>Возвращает книги, похожие на указанную (по совпадению жанров).</summary>
    Task<List<BookSummaryDto>> GetSimilarBooksAsync(Guid bookId, int limit = 6, CancellationToken ct = default);
}
