using System.Diagnostics;
using Brs.Application.DTOs.Books;
using Brs.Application.DTOs.Search;
using Brs.Application.Interfaces;
using Brs.Domain.Entities;
using Brs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brs.Infrastructure.Services;

/// <summary>Сервис расширенного поиска книг.</summary>
public class SearchService : ISearchService
{
    private const int MaxPageSize = 100;
    private const int MaxAutocomplete = 10;

    // причина: пороги похожести подобраны эмпирически — для названий мягче (короткие строки),
    // для жанров строже (их немного, нужны точные совпадения)
    private const double TrigramSearchThreshold = 0.15;
    private const double TrigramBookThreshold = 0.2;
    private const double TrigramAuthorThreshold = 0.2;
    private const double TrigramGenreThreshold = 0.3;

    // причина: триграммный поиск имеет смысл для коротких запросов (опечатки в одном слове),
    // для длинных фраз полнотекстовый поиск точнее
    private const int TrigramMaxQueryLength = 20;

    private readonly BrsDbContext _db;
    private readonly DbContextOptions<BrsDbContext> _dbOptions;

    public SearchService(BrsDbContext db, DbContextOptions<BrsDbContext> dbOptions)
    {
        _db = db;
        _dbOptions = dbOptions;
    }

    /// <inheritdoc/>
    public async Task<SearchResultDto> SearchAsync(SearchRequest request, CancellationToken ct = default)
    {
        // причина: измеряем время поиска для отдачи в SearchTimeMs (диагностика/UX)
        var stopwatch = Stopwatch.StartNew();

        var query = request.Query?.Trim() ?? string.Empty;
        var hasQuery = query.Length > 0;

        IQueryable<Book> searchQuery = _db.Books.AsNoTracking();

        if (hasQuery)
        {
            // причина: полнотекстовый поиск по tsvector (используется словарь 'russian'
            // со стеммингом). coalesce description, иначе конкатенация с null даёт null.
            var fullText = _db.Books.AsNoTracking().Where(b =>
                EF.Functions.ToTsVector("russian", b.Title + " " + (b.Description ?? ""))
                    .Matches(EF.Functions.PlainToTsQuery("russian", query)));

            if (query.Length < TrigramMaxQueryLength)
            {
                // причина: триграммы ловят опечатки; Union даёт лучшее покрытие, чем OR,
                // и UNION сам по себе устраняет дубликаты строк
                var fuzzy = _db.Books.AsNoTracking().Where(b =>
                    EF.Functions.TrigramsSimilarity(b.Title, query) > TrigramSearchThreshold);

                searchQuery = fullText.Union(fuzzy).Distinct();
            }
            else
            {
                searchQuery = fullText;
            }
        }

        searchQuery = ApplyFilters(searchQuery, request);

        var totalCount = await searchQuery.CountAsync(ct);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > MaxPageSize ? 20 : request.PageSize;

        var ordered = ApplySort(searchQuery, request.SortBy, query, hasQuery);

        // причина: Include нельзя применить после Union/Distinct, поэтому сначала
        // забираем упорядоченные Id страницы, затем догружаем связи отдельным запросом
        var pageIds = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => b.Id)
            .ToListAsync(ct);

        var items = await LoadSummariesAsync(pageIds, ct);

        stopwatch.Stop();

        return new SearchResultDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Query = request.Query ?? string.Empty,
            SearchTimeMs = stopwatch.ElapsedMilliseconds
        };
    }

    /// <inheritdoc/>
    public async Task<AutocompleteResultDto> AutocompleteAsync(AutocompleteRequest request, CancellationToken ct = default)
    {
        var query = request.Query?.Trim() ?? string.Empty;

        // причина: подсказки бессмысленны для 1 символа — слишком много шума
        if (query.Length < 2)
            return new AutocompleteResultDto();

        var limit = request.Limit is < 1 or > MaxAutocomplete ? 5 : request.Limit;

        // причина: один DbContext не поддерживает параллельные запросы, поэтому для
        // Task.WhenAll создаём отдельный контекст на каждый источник (своё подключение)
        var booksTask = QuerySuggestionsAsync(ct => SuggestBooks(ct, query, limit), ct);
        var authorsTask = QuerySuggestionsAsync(ct => SuggestAuthors(ct, query, limit), ct);
        var genresTask = QuerySuggestionsAsync(ct => SuggestGenres(ct, query, limit), ct);

        var results = await Task.WhenAll(booksTask, authorsTask, genresTask);

        // причина: сводим три источника, сортируем по похожести и берём не более limit
        var suggestions = results
            .SelectMany(r => r)
            .OrderByDescending(s => s.Score)
            .Take(limit)
            .Select(s => s.Suggestion)
            .ToList();

        return new AutocompleteResultDto { Suggestions = suggestions };
    }

    /// <inheritdoc/>
    public async Task<List<BookSummaryDto>> GetSimilarBooksAsync(Guid bookId, int limit = 6, CancellationToken ct = default)
    {
        if (limit < 1) limit = 6;

        // причина: набор жанров исходной книги — основа для поиска похожих
        var genreIds = await _db.BookGenres.AsNoTracking()
            .Where(bg => bg.BookId == bookId)
            .Select(bg => bg.GenreId)
            .ToListAsync(ct);

        // TODO: заменить на ML-based похожесть в Фазе 6 (учитывать теги, эмбеддинги описаний)
        if (genreIds.Count == 0)
            return new List<BookSummaryDto>();

        // причина: ранжируем по числу совпадающих жанров, затем по рейтингу;
        // забираем только Id, чтобы потом догрузить связи (Select мешает Include)
        var orderedIds = await _db.Books.AsNoTracking()
            .Where(b => b.Id != bookId && b.BookGenres.Any(bg => genreIds.Contains(bg.GenreId)))
            .Select(b => new
            {
                b.Id,
                MatchCount = b.BookGenres.Count(bg => genreIds.Contains(bg.GenreId)),
                b.AvgRating
            })
            .OrderByDescending(x => x.MatchCount)
            .ThenByDescending(x => x.AvgRating)
            .Take(limit)
            .Select(x => x.Id)
            .ToListAsync(ct);

        return await LoadSummariesAsync(orderedIds, ct);
    }

    // ---- helpers ----

    /// <summary>Применяет фильтры жанров/автора/языка/годов/рейтинга.</summary>
    private static IQueryable<Book> ApplyFilters(IQueryable<Book> query, SearchRequest request)
    {
        if (request.GenreIds is { Count: > 0 })
            query = query.Where(b => b.BookGenres.Any(bg => request.GenreIds.Contains(bg.GenreId)));

        if (request.AuthorId is { } authorId)
            query = query.Where(b => b.BookAuthors.Any(ba => ba.AuthorId == authorId));

        if (!string.IsNullOrWhiteSpace(request.Language))
            query = query.Where(b => b.Language == request.Language);

        if (request.YearFrom is { } yearFrom)
            query = query.Where(b => b.PublishedYear >= yearFrom);

        if (request.YearTo is { } yearTo)
            query = query.Where(b => b.PublishedYear <= yearTo);

        if (request.MinRating is { } minRating)
            query = query.Where(b => b.AvgRating >= minRating);

        return query;
    }

    /// <summary>Применяет сортировку с учётом релевантности (ts_rank) для текстового запроса.</summary>
    private static IQueryable<Book> ApplySort(IQueryable<Book> query, string? sortBy, string searchQuery, bool hasQuery)
    {
        // причина: relevance имеет смысл только при наличии текста; иначе по рейтингу
        var effective = (sortBy ?? "relevance").ToLowerInvariant();
        if (effective == "relevance" && !hasQuery)
            effective = "rating";

        return effective switch
        {
            "rating" => query.OrderByDescending(b => b.AvgRating),
            "date" => query.OrderByDescending(b => b.PublishedYear),
            "popularity" => query.OrderByDescending(b => b.RatingCount),
            // причина: relevance — сначала ts_rank полнотекстового совпадения,
            // затем триграммная похожесть названия (для нечётких попаданий)
            _ => query
                .OrderByDescending(b =>
                    EF.Functions.ToTsVector("russian", b.Title + " " + (b.Description ?? ""))
                        .Rank(EF.Functions.PlainToTsQuery("russian", searchQuery)))
                .ThenByDescending(b => EF.Functions.TrigramsSimilarity(b.Title, searchQuery))
        };
    }

    /// <summary>Загружает краткие карточки книг по списку Id с сохранением порядка.</summary>
    private async Task<List<BookSummaryDto>> LoadSummariesAsync(List<Guid> orderedIds, CancellationToken ct)
    {
        if (orderedIds.Count == 0)
            return new List<BookSummaryDto>();

        var books = await _db.Books.AsNoTracking()
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .Include(b => b.BookGenres).ThenInclude(bg => bg.Genre)
            .Where(b => orderedIds.Contains(b.Id))
            .ToListAsync(ct);

        // причина: WHERE IN не гарантирует порядок — восстанавливаем порядок ранжирования
        var byId = books.ToDictionary(b => b.Id);
        return orderedIds
            .Where(byId.ContainsKey)
            .Select(id => MapSummary(byId[id]))
            .ToList();
    }

    /// <summary>Выполняет переданный запрос подсказок в отдельном DbContext.</summary>
    private async Task<List<ScoredSuggestion>> QuerySuggestionsAsync(
        Func<BrsDbContext, Task<List<ScoredSuggestion>>> queryFunc, CancellationToken ct)
    {
        // причина: отдельный контекст обеспечивает безопасный параллелизм в Task.WhenAll
        await using var ctx = new BrsDbContext(_dbOptions);
        return await queryFunc(ctx);
    }

    private static Task<List<ScoredSuggestion>> SuggestBooks(BrsDbContext ctx, string query, int limit)
        => ctx.Books.AsNoTracking()
            .Where(b => EF.Functions.TrigramsSimilarity(b.Title, query) > TrigramBookThreshold)
            .OrderByDescending(b => EF.Functions.TrigramsSimilarity(b.Title, query))
            .Take(limit)
            .Select(b => new ScoredSuggestion(
                new SuggestionDto { Text = b.Title, Type = "book", Id = b.Id },
                EF.Functions.TrigramsSimilarity(b.Title, query)))
            .ToListAsync();

    private static Task<List<ScoredSuggestion>> SuggestAuthors(BrsDbContext ctx, string query, int limit)
        => ctx.Authors.AsNoTracking()
            .Where(a => EF.Functions.TrigramsSimilarity(a.Name, query) > TrigramAuthorThreshold)
            .OrderByDescending(a => EF.Functions.TrigramsSimilarity(a.Name, query))
            .Take(limit)
            .Select(a => new ScoredSuggestion(
                new SuggestionDto { Text = a.Name, Type = "author", Id = a.Id },
                EF.Functions.TrigramsSimilarity(a.Name, query)))
            .ToListAsync();

    private static Task<List<ScoredSuggestion>> SuggestGenres(BrsDbContext ctx, string query, int limit)
        => ctx.Genres.AsNoTracking()
            .Where(g => EF.Functions.TrigramsSimilarity(g.Name, query) > TrigramGenreThreshold)
            .OrderByDescending(g => EF.Functions.TrigramsSimilarity(g.Name, query))
            .Take(limit)
            .Select(g => new ScoredSuggestion(
                new SuggestionDto { Text = g.Name, Type = "genre", Id = g.Id },
                EF.Functions.TrigramsSimilarity(g.Name, query)))
            .ToListAsync();

    private static BookSummaryDto MapSummary(Book b) => new()
    {
        Id = b.Id,
        Title = b.Title,
        CoverUrl = b.CoverUrl,
        AvgRating = b.AvgRating,
        RatingCount = b.RatingCount,
        Authors = b.BookAuthors.Select(ba => ba.Author!.Name).ToList(),
        Genres = b.BookGenres.Select(bg => bg.Genre!.Name).ToList()
    };

    /// <summary>Подсказка вместе с оценкой похожести для межисточниковой сортировки.</summary>
    private readonly record struct ScoredSuggestion(SuggestionDto Suggestion, double Score);
}
