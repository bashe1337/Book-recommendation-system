using Brs.Application.DTOs.Books;
using Brs.Application.DTOs.Interactions;
using Brs.Application.Exceptions;
using Brs.Application.Interfaces;
using Brs.Domain.Entities;
using Brs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brs.Infrastructure.Services;

/// <summary>Сервис пользовательских взаимодействий: оценки, отзывы, избранное, история.</summary>
public class InteractionService : IInteractionService
{
    private const int MaxPageSize = 100;
    // причина: окно дедупликации просмотров — повторное открытие книги в течение
    // получаса не плодит записи в истории
    private static readonly TimeSpan ViewDedupWindow = TimeSpan.FromMinutes(30);

    private readonly BrsDbContext _db;

    public InteractionService(BrsDbContext db) => _db = db;

    // ---- Оценки ----

    /// <inheritdoc/>
    public async Task<RatingDto> RateBookAsync(Guid userId, Guid bookId, int score, CancellationToken ct = default)
    {
        if (score is < 1 or > 5)
            throw new ValidationException("Оценка должна быть от 1 до 5.");

        if (!await _db.Books.AnyAsync(b => b.Id == bookId, ct))
            throw new NotFoundException($"Книга {bookId} не найдена.");

        var rating = await _db.Ratings.FirstOrDefaultAsync(r => r.UserId == userId && r.BookId == bookId, ct);
        if (rating is null)
        {
            rating = new Rating { UserId = userId, BookId = bookId, Score = score, CreatedAt = DateTime.UtcNow };
            await _db.Ratings.AddAsync(rating, ct);
        }
        else
        {
            // причина: upsert — повторная оценка той же книги перезаписывает прежнюю
            rating.Score = score;
            rating.CreatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await RecalculateBookRatingAsync(bookId, ct);

        return MapRating(rating);
    }

    /// <inheritdoc/>
    public async Task DeleteRatingAsync(Guid userId, Guid bookId, CancellationToken ct = default)
    {
        var rating = await _db.Ratings.FirstOrDefaultAsync(r => r.UserId == userId && r.BookId == bookId, ct)
            ?? throw new NotFoundException("Оценка не найдена.");

        _db.Ratings.Remove(rating);
        await _db.SaveChangesAsync(ct);
        await RecalculateBookRatingAsync(bookId, ct);
    }

    /// <inheritdoc/>
    public async Task<RatingDto?> GetUserRatingAsync(Guid userId, Guid bookId, CancellationToken ct = default)
    {
        var rating = await _db.Ratings.AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.BookId == bookId, ct);
        return rating is null ? null : MapRating(rating);
    }

    // ---- Отзывы ----

    /// <inheritdoc/>
    public async Task<ReviewDto> CreateReviewAsync(Guid userId, Guid bookId, CreateReviewRequest request, CancellationToken ct = default)
    {
        if (!await _db.Books.AnyAsync(b => b.Id == bookId, ct))
            throw new NotFoundException($"Книга {bookId} не найдена.");

        // причина: один пользователь — один отзыв на книгу
        if (await _db.Reviews.AnyAsync(r => r.UserId == userId && r.BookId == bookId, ct))
            throw new ValidationException("Вы уже оставили отзыв на эту книгу.");

        var review = new Review
        {
            UserId = userId,
            BookId = bookId,
            Content = request.Content,
            CreatedAt = DateTime.UtcNow,
            SentimentScore = null // TODO: интегрировать анализ тональности в Фазе 6
        };
        await _db.Reviews.AddAsync(review, ct);
        await _db.SaveChangesAsync(ct);

        var username = await _db.Users.Where(u => u.Id == userId).Select(u => u.Username).FirstAsync(ct);
        return MapReview(review, username);
    }

    /// <inheritdoc/>
    public async Task<ReviewDto> UpdateReviewAsync(Guid userId, Guid reviewId, UpdateReviewRequest request, CancellationToken ct = default)
    {
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId, ct)
            ?? throw new NotFoundException($"Отзыв {reviewId} не найден.");

        // причина: редактировать отзыв может только его автор
        if (review.UserId != userId)
            throw new UnauthorizedException("Вы можете редактировать только свои отзывы.");

        review.Content = request.Content;
        // TODO: сбросить SentimentScore в null для пересчёта после интеграции ML
        await _db.SaveChangesAsync(ct);

        var username = await _db.Users.Where(u => u.Id == userId).Select(u => u.Username).FirstAsync(ct);
        return MapReview(review, username);
    }

    /// <inheritdoc/>
    public async Task DeleteReviewAsync(Guid userId, Guid reviewId, bool isAdmin = false, CancellationToken ct = default)
    {
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId, ct)
            ?? throw new NotFoundException($"Отзыв {reviewId} не найден.");

        // причина: удалить отзыв может автор или администратор
        if (review.UserId != userId && !isAdmin)
            throw new UnauthorizedException("Вы можете удалять только свои отзывы.");

        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<ReviewDto>> GetBookReviewsAsync(Guid bookId, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Normalize(page, pageSize);

        var query = _db.Reviews.AsNoTracking().Where(r => r.BookId == bookId);
        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            // причина: проецируем сразу в DTO с Username через join на Users — без N+1
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                BookId = r.BookId,
                UserId = r.UserId,
                Username = r.User!.Username,
                Content = r.Content,
                CreatedAt = r.CreatedAt,
                SentimentScore = r.SentimentScore
            })
            .ToListAsync(ct);

        return Page(items, total, page, pageSize);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<ReviewDto>> GetUserReviewsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Normalize(page, pageSize);

        var query = _db.Reviews.AsNoTracking().Where(r => r.UserId == userId);
        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                BookId = r.BookId,
                UserId = r.UserId,
                Username = r.User!.Username,
                Content = r.Content,
                CreatedAt = r.CreatedAt,
                SentimentScore = r.SentimentScore
            })
            .ToListAsync(ct);

        return Page(items, total, page, pageSize);
    }

    // ---- Избранное ----

    /// <inheritdoc/>
    public async Task AddToFavoritesAsync(Guid userId, Guid bookId, CancellationToken ct = default)
    {
        if (!await _db.Books.AnyAsync(b => b.Id == bookId, ct))
            throw new NotFoundException($"Книга {bookId} не найдена.");

        // причина: идемпотентность — повторное добавление не создаёт дубль и не падает
        if (await IsBookFavoritedAsync(userId, bookId, ct))
            return;

        await _db.Favorites.AddAsync(new Favorite
        {
            UserId = userId, BookId = bookId, AddedAt = DateTime.UtcNow
        }, ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task RemoveFromFavoritesAsync(Guid userId, Guid bookId, CancellationToken ct = default)
    {
        var favorite = await _db.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.BookId == bookId, ct);
        // причина: удаление отсутствующего — не ошибка (идемпотентно)
        if (favorite is null) return;

        _db.Favorites.Remove(favorite);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<FavoriteDto>> GetUserFavoritesAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Normalize(page, pageSize);

        var query = _db.Favorites.AsNoTracking().Where(f => f.UserId == userId);
        var total = await query.CountAsync(ct);

        var favorites = await query
            .OrderByDescending(f => f.AddedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(f => f.Book!).ThenInclude(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .Include(f => f.Book!).ThenInclude(b => b.BookGenres).ThenInclude(bg => bg.Genre)
            .ToListAsync(ct);

        var items = favorites.Select(f => new FavoriteDto
        {
            Id = f.Id,
            BookId = f.BookId,
            AddedAt = f.AddedAt,
            Book = MapSummary(f.Book!)
        }).ToList();

        return Page(items, total, page, pageSize);
    }

    /// <inheritdoc/>
    public async Task<bool> IsBookFavoritedAsync(Guid userId, Guid bookId, CancellationToken ct = default)
        => await _db.Favorites.AnyAsync(f => f.UserId == userId && f.BookId == bookId, ct);

    // ---- История просмотров ----

    /// <inheritdoc/>
    public async Task RecordViewAsync(Guid userId, Guid bookId, CancellationToken ct = default)
    {
        // причина: НЕ проверяем существование книги — метод вызывается часто,
        // производительность важнее; невалидный bookId просто не создаст полезной записи
        var since = DateTime.UtcNow - ViewDedupWindow;
        var recentlyViewed = await _db.ViewHistory.AnyAsync(
            v => v.UserId == userId && v.BookId == bookId && v.ViewedAt >= since, ct);
        if (recentlyViewed) return;

        await _db.ViewHistory.AddAsync(new ViewHistory
        {
            UserId = userId, BookId = bookId, ViewedAt = DateTime.UtcNow
        }, ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<ViewHistoryDto>> GetUserViewHistoryAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Normalize(page, pageSize);

        var query = _db.ViewHistory.AsNoTracking().Where(v => v.UserId == userId);
        var total = await query.CountAsync(ct);

        var history = await query
            .OrderByDescending(v => v.ViewedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(v => v.Book!).ThenInclude(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .Include(v => v.Book!).ThenInclude(b => b.BookGenres).ThenInclude(bg => bg.Genre)
            .ToListAsync(ct);

        var items = history.Select(v => new ViewHistoryDto
        {
            Id = v.Id,
            BookId = v.BookId,
            ViewedAt = v.ViewedAt,
            Book = MapSummary(v.Book!)
        }).ToList();

        return Page(items, total, page, pageSize);
    }

    /// <inheritdoc/>
    public async Task ClearViewHistoryAsync(Guid userId, CancellationToken ct = default)
        // причина: ExecuteDelete — массовое удаление одним SQL без загрузки строк в память
        => await _db.ViewHistory.Where(v => v.UserId == userId).ExecuteDeleteAsync(ct);

    // ---- Статистика ----

    /// <inheritdoc/>
    public async Task<BookStatsDto> GetBookStatsAsync(Guid bookId, CancellationToken ct = default)
    {
        if (!await _db.Books.AnyAsync(b => b.Id == bookId, ct))
            throw new NotFoundException($"Книга {bookId} не найдена.");

        var ratingCount = await _db.Ratings.CountAsync(r => r.BookId == bookId, ct);
        var avg = ratingCount > 0
            ? await _db.Ratings.Where(r => r.BookId == bookId).AverageAsync(r => (double)r.Score, ct)
            : 0d;

        return new BookStatsDto
        {
            BookId = bookId,
            AvgRating = (float)avg,
            RatingCount = ratingCount,
            ReviewCount = await _db.Reviews.CountAsync(r => r.BookId == bookId, ct),
            FavoriteCount = await _db.Favorites.CountAsync(f => f.BookId == bookId, ct)
        };
    }

    // ---- helpers ----

    /// <summary>Пересчитывает денормализованные AvgRating/RatingCount в таблице books.</summary>
    private async Task RecalculateBookRatingAsync(Guid bookId, CancellationToken ct)
    {
        // причина: держим агрегат в books для быстрой сортировки каталога без JOIN.
        // Считаем в C# через индексированные запросы — устойчиво к именам колонок.
        var count = await _db.Ratings.CountAsync(r => r.BookId == bookId, ct);
        var avg = count > 0
            ? await _db.Ratings.Where(r => r.BookId == bookId).AverageAsync(r => (double)r.Score, ct)
            : 0d;

        var book = await _db.Books.FirstAsync(b => b.Id == bookId, ct);
        book.AvgRating = avg;
        book.RatingCount = count;
        await _db.SaveChangesAsync(ct);
    }

    private static (int page, int pageSize) Normalize(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > MaxPageSize) pageSize = 20;
        return (page, pageSize);
    }

    private static PagedResult<T> Page<T>(List<T> items, int total, int page, int pageSize)
        => new() { Items = items, TotalCount = total, Page = page, PageSize = pageSize };

    private static RatingDto MapRating(Rating r) => new()
    {
        Id = r.Id, BookId = r.BookId, UserId = r.UserId, Score = r.Score, CreatedAt = r.CreatedAt
    };

    private static ReviewDto MapReview(Review r, string username) => new()
    {
        Id = r.Id, BookId = r.BookId, UserId = r.UserId, Username = username,
        Content = r.Content, CreatedAt = r.CreatedAt, SentimentScore = r.SentimentScore
    };

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
