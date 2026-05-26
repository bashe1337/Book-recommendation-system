using Brs.Application.DTOs.Books;
using Brs.Application.DTOs.Interactions;

namespace Brs.Application.Interfaces;

/// <summary>Сценарии пользовательских взаимодействий: оценки, отзывы, избранное, история.</summary>
public interface IInteractionService
{
    // ---- Оценки ----

    /// <summary>Создаёт или обновляет оценку книги пользователем (upsert).</summary>
    Task<RatingDto> RateBookAsync(Guid userId, Guid bookId, int score, CancellationToken ct = default);

    /// <summary>Удаляет оценку пользователя для книги.</summary>
    Task DeleteRatingAsync(Guid userId, Guid bookId, CancellationToken ct = default);

    /// <summary>Возвращает оценку пользователя для книги или null.</summary>
    Task<RatingDto?> GetUserRatingAsync(Guid userId, Guid bookId, CancellationToken ct = default);

    // ---- Отзывы ----

    /// <summary>Создаёт отзыв пользователя о книге.</summary>
    Task<ReviewDto> CreateReviewAsync(Guid userId, Guid bookId, CreateReviewRequest request, CancellationToken ct = default);

    /// <summary>Редактирует отзыв (только автор).</summary>
    Task<ReviewDto> UpdateReviewAsync(Guid userId, Guid reviewId, UpdateReviewRequest request, CancellationToken ct = default);

    /// <summary>Удаляет отзыв (автор или администратор).</summary>
    Task DeleteReviewAsync(Guid userId, Guid reviewId, bool isAdmin = false, CancellationToken ct = default);

    /// <summary>Возвращает страницу отзывов книги (по дате, новые сверху).</summary>
    Task<PagedResult<ReviewDto>> GetBookReviewsAsync(Guid bookId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Возвращает страницу отзывов пользователя.</summary>
    Task<PagedResult<ReviewDto>> GetUserReviewsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);

    // ---- Избранное ----

    /// <summary>Добавляет книгу в избранное (идемпотентно).</summary>
    Task AddToFavoritesAsync(Guid userId, Guid bookId, CancellationToken ct = default);

    /// <summary>Убирает книгу из избранного.</summary>
    Task RemoveFromFavoritesAsync(Guid userId, Guid bookId, CancellationToken ct = default);

    /// <summary>Возвращает страницу избранного пользователя.</summary>
    Task<PagedResult<FavoriteDto>> GetUserFavoritesAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Проверяет, в избранном ли книга у пользователя.</summary>
    Task<bool> IsBookFavoritedAsync(Guid userId, Guid bookId, CancellationToken ct = default);

    // ---- История просмотров ----

    /// <summary>Фиксирует просмотр книги с дедупликацией по окну в 30 минут.</summary>
    Task RecordViewAsync(Guid userId, Guid bookId, CancellationToken ct = default);

    /// <summary>Возвращает страницу истории просмотров.</summary>
    Task<PagedResult<ViewHistoryDto>> GetUserViewHistoryAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Очищает всю историю просмотров пользователя.</summary>
    Task ClearViewHistoryAsync(Guid userId, CancellationToken ct = default);

    // ---- Статистика ----

    /// <summary>Возвращает агрегированную статистику книги.</summary>
    Task<BookStatsDto> GetBookStatsAsync(Guid bookId, CancellationToken ct = default);
}
