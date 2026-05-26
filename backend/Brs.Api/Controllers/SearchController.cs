using Brs.Application.DTOs.Books;
using Brs.Application.DTOs.Search;
using Brs.Application.Exceptions;
using Brs.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Brs.Api.Controllers;

/// <summary>Расширенный поиск книг, автодополнение и похожие книги.</summary>
[ApiController]
[Route("api")]
[AllowAnonymous]
public class SearchController : ControllerBase
{
    // причина: кэш автодополнения живёт 5 минут — запрос летит при каждом нажатии клавиши
    private static readonly TimeSpan AutocompleteCacheTtl = TimeSpan.FromMinutes(5);

    private readonly ISearchService _search;
    private readonly IMemoryCache _cache;

    public SearchController(ISearchService search, IMemoryCache cache)
    {
        _search = search;
        _cache = cache;
    }

    /// <summary>GET /api/search — поиск книг с фильтрами и сортировкой.</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(SearchResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SearchResultDto>> Search([FromQuery] SearchRequest request, CancellationToken ct)
    {
        // причина: пустой запрос без фильтров вернул бы весь каталог — это ошибка ввода
        if (string.IsNullOrWhiteSpace(request.Query) && !request.HasAnyFilter)
            throw new ValidationException("Укажите поисковый запрос или хотя бы один фильтр.");

        return Ok(await _search.SearchAsync(request, ct));
    }

    /// <summary>GET /api/search/autocomplete — подсказки по книгам, авторам и жанрам.</summary>
    [HttpGet("search/autocomplete")]
    [ProducesResponseType(typeof(AutocompleteResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AutocompleteResultDto>> Autocomplete(
        [FromQuery] AutocompleteRequest request, CancellationToken ct)
    {
        var query = request.Query?.Trim() ?? string.Empty;

        // причина: для короткого запроса сразу пустой результат — не трогаем кэш и БД
        if (query.Length < 2)
            return Ok(new AutocompleteResultDto());

        var cacheKey = $"autocomplete:{query.ToLowerInvariant()}:{request.Limit}";
        if (_cache.TryGetValue(cacheKey, out AutocompleteResultDto? cached) && cached is not null)
            return Ok(cached);

        var result = await _search.AutocompleteAsync(request, ct);
        _cache.Set(cacheKey, result, AutocompleteCacheTtl);

        return Ok(result);
    }

    /// <summary>GET /api/books/{bookId}/similar — похожие книги по жанрам.</summary>
    [HttpGet("books/{bookId:guid}/similar")]
    [ProducesResponseType(typeof(List<BookSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BookSummaryDto>>> Similar(
        Guid bookId, [FromQuery] int limit = 6, CancellationToken ct = default)
    {
        // причина: ограничиваем сверху, чтобы не отдавать слишком большую карусель
        if (limit is < 1 or > 12) limit = 6;
        return Ok(await _search.GetSimilarBooksAsync(bookId, limit, ct));
    }
}
