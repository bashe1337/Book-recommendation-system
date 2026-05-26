using Brs.Application.DTOs.Books;
using Brs.Application.DTOs.Recommendations;
using Brs.Application.Exceptions;
using Brs.Application.Interfaces;
using Brs.Domain.Entities;
using Brs.Infrastructure.ExternalApis.Recommendations;
using Brs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Brs.Infrastructure.Services;

/// <summary>
/// Главный оркестратор рекомендаций. Решает, какую модель использовать,
/// маппит ответы Python в BookSummaryDto, обеспечивает fallback на популярное,
/// инициирует ленивое обучение, кэширует «рекомендацию дня».
/// </summary>
public class RecommendationService : IRecommendationService
{
    // причина: ключи кэша — общий префикс, чтобы при необходимости массово инвалидировать
    private const string DailyCachePrefix = "rec:daily:";

    private readonly BrsDbContext _db;
    private readonly RecommendationsClient _client;
    private readonly RecommendationsTrainCoordinator _trainer;
    private readonly RecommendationsOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RecommendationService> _logger;

    public RecommendationService(
        BrsDbContext db,
        RecommendationsClient client,
        RecommendationsTrainCoordinator trainer,
        IOptions<RecommendationsOptions> options,
        IMemoryCache cache,
        ILogger<RecommendationService> logger)
    {
        _db = db;
        _client = client;
        _trainer = trainer;
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    // =====================================================================
    // For You — SVD при ≥N оценок, иначе content-based
    // =====================================================================

    /// <inheritdoc/>
    public async Task<RecommendationListDto> GetForYouAsync(Guid userId, int limit, CancellationToken ct = default)
    {
        var ratingsCount = await _db.Ratings.CountAsync(r => r.UserId == userId, ct);
        var useSvd = ratingsCount >= _options.SvdThresholdRatings;

        var primaryModel = useSvd ? RecommendationModel.Svd : RecommendationModel.Content;
        var explanation = useSvd
            ? $"Подобрано SVD-моделью на основе ваших {ratingsCount} оценок"
            : $"Подобрано по содержимому книг ({ratingsCount} из {_options.SvdThresholdRatings} оценок для SVD — продолжайте оценивать!)";

        return await CallPerUserOrFallbackAsync(userId, limit, primaryModel, explanation, ct);
    }

    // =====================================================================
    // May Like — ALS
    // =====================================================================

    /// <inheritdoc/>
    public Task<RecommendationListDto> GetMayLikeAsync(Guid userId, int limit, CancellationToken ct = default)
        => CallPerUserOrFallbackAsync(
            userId, limit, RecommendationModel.Als,
            "ALS — учитывает ваши просмотры, избранное и оценки", ct);

    // =====================================================================
    // Based on my ratings — content-based по случайным оценённым книгам
    // =====================================================================

    /// <inheritdoc/>
    public async Task<BasedOnMyRatingsResponseDto> GetBasedOnMyRatingsAsync(
        Guid userId, int sourcesCount, int perSourceLimit, CancellationToken ct = default)
    {
        if (sourcesCount < 1) sourcesCount = 10;
        if (perSourceLimit < 1) perSourceLimit = 10;

        // причина: берём ВСЕ ID оценённых книг, потом случайно выбираем sourcesCount
        // (если оценок меньше — все идут в выборку; на каждом рефреше — другой порядок)
        var ratedBookIds = await _db.Ratings.AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => r.BookId)
            .ToListAsync(ct);

        if (ratedBookIds.Count == 0)
            return new BasedOnMyRatingsResponseDto { Source = RecommendationSource.Fallback };

        var rng = Random.Shared;
        var sources = ratedBookIds
            .OrderBy(_ => rng.Next())
            .Take(sourcesCount)
            .ToList();

        // причина: догрузим карточки книг-источников одним SQL — порядок восстановим
        var sourceBooks = await LoadSummariesAsync(sources, ct);
        var sourceById = sourceBooks.ToDictionary(b => b.Id);

        var groups = new List<BasedOnRatedBookDto>();
        var anyMl = false;

        foreach (var sourceId in sources)
        {
            if (!sourceById.TryGetValue(sourceId, out var sourceDto)) continue;

            var result = await _client.ContentSimilarAsync(sourceId, perSourceLimit, ct);
            List<BookSummaryDto> items;
            if (result.Status == PythonCallStatus.Ok && result.Value is { Count: > 0 })
            {
                items = await ResolveItemsAsync(result.Value, perSourceLimit, ct);
                if (items.Count > 0) anyMl = true;
            }
            else
            {
                // причина: фолбэк на жанровое сходство одной книги (уже есть в SearchService логика,
                // но дублируем минимальной SQL-выборкой, чтобы не плодить зависимости между сервисами)
                items = await GenreFallbackForBookAsync(sourceId, perSourceLimit, ct);
                if (result.Status == PythonCallStatus.NotTrained)
                    _ = _trainer.EnsureTrainingAsync(RecommendationModel.Content, ct);
            }

            // причина: исключаем сам источник и уже оценённые пользователем книги
            var ratedSet = ratedBookIds.ToHashSet();
            items = items.Where(b => b.Id != sourceId && !ratedSet.Contains(b.Id)).ToList();

            if (items.Count == 0) continue;

            groups.Add(new BasedOnRatedBookDto
            {
                Source = sourceDto,
                Model = RecommendationModel.Content,
                Items = items
            });
        }

        return new BasedOnMyRatingsResponseDto
        {
            Groups = groups,
            Source = anyMl ? RecommendationSource.Ml : RecommendationSource.Fallback
        };
    }

    // =====================================================================
    // Similar to book — content-based (для блока на странице поиска)
    // =====================================================================

    /// <inheritdoc/>
    public async Task<RecommendationListDto> GetSimilarToBookAsync(Guid bookId, int limit, CancellationToken ct = default)
    {
        var result = await _client.ContentSimilarAsync(bookId, limit, ct);
        return await BuildFromSimilarAsync(
            result, limit, RecommendationModel.Content,
            "Похожие книги по описанию, жанрам и тегам (content-based)",
            triggerTrain: RecommendationModel.Content,
            fallbackBookId: bookId, ct: ct);
    }

    // =====================================================================
    // Co-readers — item-CF (блок «С этой книгой читают»)
    // =====================================================================

    /// <inheritdoc/>
    public async Task<RecommendationListDto> GetCoReadersAsync(Guid bookId, int limit, CancellationToken ct = default)
    {
        var result = await _client.ItemCfSimilarAsync(bookId, limit, ct);
        return await BuildFromSimilarAsync(
            result, limit, RecommendationModel.ItemCf,
            "С этой книгой читают (item-based CF: совпадение поведения читателей)",
            triggerTrain: RecommendationModel.ItemCf,
            fallbackBookId: bookId, ct: ct);
    }

    // =====================================================================
    // Popular — SQL без ML
    // =====================================================================

    /// <inheritdoc/>
    public async Task<RecommendationListDto> GetPopularAsync(int limit, CancellationToken ct = default)
    {
        if (limit < 1) limit = 10;

        // причина: классический скор популярности — AvgRating, но взвешенный
        // логарифмом числа оценок, чтобы единичные пятёрки не вылезали наверх
        var orderedIds = await _db.Books.AsNoTracking()
            .Where(b => b.RatingCount > 0)
            .OrderByDescending(b => b.AvgRating * Math.Log(b.RatingCount + 1))
            .ThenByDescending(b => b.RatingCount)
            .Select(b => b.Id)
            .Take(limit)
            .ToListAsync(ct);

        // причина: если оценок ещё нет ни у кого (свежий запуск) — отдаём просто свежие книги
        if (orderedIds.Count == 0)
        {
            orderedIds = await _db.Books.AsNoTracking()
                .OrderByDescending(b => b.Id)
                .Select(b => b.Id)
                .Take(limit)
                .ToListAsync(ct);
        }

        var items = await LoadSummariesAsync(orderedIds, ct);
        return new RecommendationListDto
        {
            Items = items,
            Model = RecommendationModel.Popularity,
            Source = RecommendationSource.Ml,  // популярность — самостоятельный алгоритм, не fallback
            Explanation = "Популярное по среднему рейтингу и числу оценок"
        };
    }

    // =====================================================================
    // By preferred genres — content-based через «якорные» книги
    // =====================================================================

    /// <inheritdoc/>
    public async Task<RecommendationListDto> GetByPreferredGenresAsync(Guid userId, int limit, CancellationToken ct = default)
    {
        if (limit < 1) limit = 10;

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException($"Пользователь {userId} не найден.");

        // причина: если жанровые предпочтения не заданы — fallback на популярное
        if (user.PreferredGenreIds is null || user.PreferredGenreIds.Count == 0)
            return await PopularFallbackAsync("Жанровые предпочтения не заданы — показано популярное", limit, ct);

        // причина: 3 «якоря» на жанр — топ по рейтингу. По каждому дёрнем content-based similar,
        // объединим со скорами, отдадим топ-limit. Это и есть content-based по предпочтениям.
        const int anchorsPerGenre = 3;
        var anchorIds = new List<Guid>();
        foreach (var genreId in user.PreferredGenreIds)
        {
            var anchors = await _db.BookGenres.AsNoTracking()
                .Where(bg => bg.GenreId == genreId)
                .OrderByDescending(bg => bg.Book!.AvgRating * (bg.Book.RatingCount + 1))
                .Take(anchorsPerGenre)
                .Select(bg => bg.BookId)
                .ToListAsync(ct);
            anchorIds.AddRange(anchors);
        }

        if (anchorIds.Count == 0)
            return await PopularFallbackAsync("В выбранных жанрах ещё нет книг — показано популярное", limit, ct);

        // причина: для каждого anchor дёрнем similar; собираем скоры по book_id, потом ранжируем
        var scores = new Dictionary<Guid, double>();
        var anyMl = false;

        foreach (var anchor in anchorIds.Distinct())
        {
            var result = await _client.ContentSimilarAsync(anchor, limit, ct);
            if (result.Status == PythonCallStatus.NotTrained)
                _ = _trainer.EnsureTrainingAsync(RecommendationModel.Content, ct);

            if (result.Status != PythonCallStatus.Ok || result.Value is null) continue;
            anyMl = true;

            foreach (var item in result.Value)
            {
                if (!Guid.TryParse(item.BookId, out var id)) continue;
                scores[id] = scores.TryGetValue(id, out var prev) ? prev + item.Score : item.Score;
            }
        }

        // причина: исключаем сами anchor-книги, уже оценённые и уже в избранном
        var rated = await _db.Ratings.AsNoTracking().Where(r => r.UserId == userId).Select(r => r.BookId).ToListAsync(ct);
        var favs = await _db.Favorites.AsNoTracking().Where(f => f.UserId == userId).Select(f => f.BookId).ToListAsync(ct);
        var excludeSet = new HashSet<Guid>(anchorIds.Concat(rated).Concat(favs));

        var orderedIds = scores
            .Where(kv => !excludeSet.Contains(kv.Key))
            .OrderByDescending(kv => kv.Value)
            .Take(limit)
            .Select(kv => kv.Key)
            .ToList();

        if (orderedIds.Count == 0)
            return await PopularFallbackAsync("По вашим жанрам пока недостаточно данных — показано популярное", limit, ct);

        var items = await LoadSummariesAsync(orderedIds, ct);
        return new RecommendationListDto
        {
            Items = items,
            Model = RecommendationModel.Content,
            Source = anyMl ? RecommendationSource.Ml : RecommendationSource.Fallback,
            Explanation = "Похожее на лучшие книги выбранных при регистрации жанров (content-based)"
        };
    }

    // =====================================================================
    // Similar Readers — User-CF
    // =====================================================================

    /// <inheritdoc/>
    public Task<RecommendationListDto> GetSimilarReadersAsync(Guid userId, int limit, CancellationToken ct = default)
        => CallPerUserOrFallbackAsync(
            userId, limit, RecommendationModel.UserCf,
            "Любят те же книги, что и вы (User-based CF)", ct);

    // =====================================================================
    // Daily — авто-выбор лучшей модели по NDCG@10, кэш 24ч
    // =====================================================================

    /// <inheritdoc/>
    public async Task<DailyRecommendationDto> GetDailyAsync(Guid userId, CancellationToken ct = default)
    {
        // причина: кэш по (userId, дате UTC) — один и тот же ответ весь день
        var today = DateTime.UtcNow.Date;
        var cacheKey = $"{DailyCachePrefix}{userId}:{today:yyyyMMdd}";

        if (_cache.TryGetValue(cacheKey, out DailyRecommendationDto? cached) && cached is not null)
            return cached;

        // причина: спрашиваем метрики у моделей с train/test split (content-based их не считает).
        // NDCG@10 предпочтительнее, fallback на Precision@10, потом на отрицательный RMSE.
        var candidates = new[]
        {
            RecommendationModel.Svd,
            RecommendationModel.Als,
            RecommendationModel.UserCf,
            RecommendationModel.ItemCf
        };

        RecommendationModel bestModel = RecommendationModel.Content;
        double bestScore = double.NegativeInfinity;

        foreach (var model in candidates)
        {
            var metrics = await _client.MetricsAsync(model, ct);
            if (metrics.Status != PythonCallStatus.Ok || metrics.Value is null) continue;

            // причина: чем больше — тем лучше; для RMSE/MAE инвертируем знак
            var score = metrics.Value.NdcgAt10
                ?? metrics.Value.PrecisionAt10
                ?? (metrics.Value.Rmse.HasValue ? -metrics.Value.Rmse.Value : (double?)null);
            if (score is null) continue;

            if (score.Value > bestScore)
            {
                bestScore = score.Value;
                bestModel = model;
            }
        }

        // причина: даже если ни одна продвинутая модель не доступна — content-based на старте
        // всегда обучается, она будет дефолтом
        var list = await CallPerUserOrFallbackAsync(userId, limit: 10, bestModel,
            $"Рекомендация дня от модели {bestModel} (лучшая по метрикам)", ct);

        // причина: одна книга из топа, выбор детерминирован датой+userId, чтобы был стабилен сутки
        var index = list.Items.Count == 0
            ? 0
            : (int)(HashUserDate(userId, today) % (uint)list.Items.Count);

        var book = list.Items.Count > 0 ? list.Items[index] : new BookSummaryDto();

        var result = new DailyRecommendationDto
        {
            Book = book,
            Model = list.Model,
            Date = today,
            Source = list.Source
        };

        _cache.Set(cacheKey, result, TimeSpan.FromHours(_options.DailyCacheHours));
        return result;
    }

    // =====================================================================
    // Health
    // =====================================================================

    /// <inheritdoc/>
    public async Task<RecommendationsHealthDto> GetHealthAsync(CancellationToken ct = default)
    {
        var health = await _client.HealthAsync(ct);
        var dto = new RecommendationsHealthDto
        {
            ServiceAvailable = health.Status == PythonCallStatus.Ok
        };

        // причина: content-based — отдельный health-ответ, метрик у неё нет
        dto.Models["content"] = new ModelHealth
        {
            Fitted = health.Value?.ModelFitted ?? false,
            BookCount = health.Value?.BookCount
        };

        var modelsWithMetrics = new[]
        {
            RecommendationModel.Svd,
            RecommendationModel.Als,
            RecommendationModel.UserCf,
            RecommendationModel.ItemCf
        };
        foreach (var model in modelsWithMetrics)
        {
            var metrics = await _client.MetricsAsync(model, ct);
            var (name, value) = PickMainMetric(metrics.Value);
            dto.Models[model.ToString().ToLowerInvariant()] = new ModelHealth
            {
                Fitted = metrics.Status == PythonCallStatus.Ok,
                Metric = name,
                MetricValue = value
            };
        }
        return dto;
    }

    /// <inheritdoc/>
    public async Task TriggerRetrainAsync(RecommendationModel? model, CancellationToken ct = default)
    {
        if (model.HasValue)
        {
            await _trainer.EnsureTrainingAsync(model.Value, ct);
            return;
        }
        // причина: без параметра — переобучаем все известные модели параллельно
        var all = new[]
        {
            RecommendationModel.Content,
            RecommendationModel.Svd,
            RecommendationModel.Als,
            RecommendationModel.UserCf,
            RecommendationModel.ItemCf
        };
        var tasks = all.Select(m => _trainer.EnsureTrainingAsync(m, ct)).ToArray();
        await Task.WhenAll(tasks);
    }

    // =====================================================================
    // helpers — общий вызов per-user моделей с fallback
    // =====================================================================

    /// <summary>Шаблонный сценарий: попробовать модель → не вышло → fallback на популярное.</summary>
    private async Task<RecommendationListDto> CallPerUserOrFallbackAsync(
        Guid userId, int limit, RecommendationModel model, string explanation, CancellationToken ct)
    {
        if (limit < 1) limit = 10;

        var result = await _client.RecommendForUserAsync(model, userId, limit, ct);

        if (result.Status == PythonCallStatus.Ok && result.Value is { Count: > 0 })
        {
            var items = await ResolveItemsAsync(result.Value, limit, ct);
            if (items.Count > 0)
            {
                return new RecommendationListDto
                {
                    Items = items,
                    Model = model,
                    Source = RecommendationSource.Ml,
                    Explanation = explanation
                };
            }
        }

        // причина: модель не обучена → запускаем тренировку в фоне, чтобы следующий запрос был «настоящим»
        if (result.Status == PythonCallStatus.NotTrained)
        {
            _logger.LogInformation("Модель {Model} не обучена — запуск фонового train", model);
            _ = _trainer.EnsureTrainingAsync(model, ct);
        }

        var fallback = await GetPopularAsync(limit, ct);
        fallback.Source = RecommendationSource.Fallback;
        fallback.Explanation = result.Status switch
        {
            PythonCallStatus.NotTrained => $"Модель {model} ещё обучается — показано популярное",
            PythonCallStatus.NotFound => $"Вы пока не в обучающей выборке {model} — показано популярное",
            PythonCallStatus.Error => $"ML-сервис недоступен — показано популярное",
            _ => "Показано популярное"
        };
        return fallback;
    }

    /// <summary>Шаблонный сценарий для similar-эндпоинтов (по book_id).</summary>
    private async Task<RecommendationListDto> BuildFromSimilarAsync(
        PythonResult<List<PythonRecommendationItem>> result, int limit,
        RecommendationModel model, string explanation, RecommendationModel triggerTrain,
        Guid fallbackBookId, CancellationToken ct)
    {
        if (result.Status == PythonCallStatus.Ok && result.Value is { Count: > 0 })
        {
            var items = await ResolveItemsAsync(result.Value, limit, ct);
            if (items.Count > 0)
            {
                return new RecommendationListDto
                {
                    Items = items,
                    Model = model,
                    Source = RecommendationSource.Ml,
                    Explanation = explanation
                };
            }
        }

        if (result.Status == PythonCallStatus.NotTrained)
            _ = _trainer.EnsureTrainingAsync(triggerTrain, ct);

        // причина: для similar-блока fallback — похожие по жанрам той же книги (не популярное —
        // популярное на странице книги выглядит бессвязно)
        var fallbackItems = await GenreFallbackForBookAsync(fallbackBookId, limit, ct);
        return new RecommendationListDto
        {
            Items = fallbackItems,
            Model = RecommendationModel.Content,
            Source = RecommendationSource.Fallback,
            Explanation = "ML недоступна — показано похожее по жанрам"
        };
    }

    /// <summary>Жанровый fallback для конкретной книги (использует те же связи, что SearchService).</summary>
    private async Task<List<BookSummaryDto>> GenreFallbackForBookAsync(Guid bookId, int limit, CancellationToken ct)
    {
        var genreIds = await _db.BookGenres.AsNoTracking()
            .Where(bg => bg.BookId == bookId)
            .Select(bg => bg.GenreId)
            .ToListAsync(ct);
        if (genreIds.Count == 0) return new List<BookSummaryDto>();

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

    /// <summary>Маппинг ответа Python в BookSummaryDto с сохранением порядка скоринга.</summary>
    private async Task<List<BookSummaryDto>> ResolveItemsAsync(
        List<PythonRecommendationItem> items, int limit, CancellationToken ct)
    {
        var orderedIds = items
            .Select(i => Guid.TryParse(i.BookId, out var g) ? (Guid?)g : null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .Take(limit)
            .ToList();
        return await LoadSummariesAsync(orderedIds, ct);
    }

    /// <summary>Загружает BookSummaryDto по списку Id с сохранением порядка.</summary>
    private async Task<List<BookSummaryDto>> LoadSummariesAsync(List<Guid> orderedIds, CancellationToken ct)
    {
        if (orderedIds.Count == 0) return new List<BookSummaryDto>();

        var books = await _db.Books.AsNoTracking()
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .Include(b => b.BookGenres).ThenInclude(bg => bg.Genre)
            .Where(b => orderedIds.Contains(b.Id))
            .ToListAsync(ct);

        var byId = books.ToDictionary(b => b.Id);
        return orderedIds.Where(byId.ContainsKey).Select(id => MapSummary(byId[id])).ToList();
    }

    private async Task<RecommendationListDto> PopularFallbackAsync(string explanation, int limit, CancellationToken ct)
    {
        var pop = await GetPopularAsync(limit, ct);
        pop.Source = RecommendationSource.Fallback;
        pop.Explanation = explanation;
        return pop;
    }

    private static (string? name, double? value) PickMainMetric(PythonMetricsResponse? m)
    {
        if (m is null) return (null, null);
        if (m.NdcgAt10.HasValue) return ("ndcg@10", m.NdcgAt10);
        if (m.PrecisionAt10.HasValue) return ("precision@10", m.PrecisionAt10);
        if (m.Rmse.HasValue) return ("rmse", m.Rmse);
        return (null, null);
    }

    /// <summary>Стабильный хеш (userId, date) → uint для детерминированного выбора книги дня.</summary>
    private static uint HashUserDate(Guid userId, DateTime date)
    {
        unchecked
        {
            // причина: HashCode.Combine достаточен — нужна детерминированность в рамках процесса
            return (uint)HashCode.Combine(userId, date);
        }
    }

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
}
