using Brs.Application.DTOs.Recommendations;

namespace Brs.Application.Interfaces;

/// <summary>
/// Оркестратор рекомендательной системы: выбор алгоритма, общение с Python-сервисом,
/// fallback на популярное, кэширование «рекомендации дня».
/// </summary>
public interface IRecommendationService
{
    /// <summary>Блок «Рекомендовано вам» (SVD при ≥ N оценок, иначе content-based).</summary>
    Task<RecommendationListDto> GetForYouAsync(Guid userId, int limit, CancellationToken ct = default);

    /// <summary>Блок «Вам может понравиться» (ALS).</summary>
    Task<RecommendationListDto> GetMayLikeAsync(Guid userId, int limit, CancellationToken ct = default);

    /// <summary>Блок «Похожее на оценённые книги» (content-based по случайным книгам пользователя).</summary>
    Task<BasedOnMyRatingsResponseDto> GetBasedOnMyRatingsAsync(Guid userId, int sourcesCount, int perSourceLimit, CancellationToken ct = default);

    /// <summary>Блок «Рекомендуем похожее» на странице поиска (content-based).</summary>
    Task<RecommendationListDto> GetSimilarToBookAsync(Guid bookId, int limit, CancellationToken ct = default);

    /// <summary>Блок «С этой книгой читают» на странице книги (item-based CF).</summary>
    Task<RecommendationListDto> GetCoReadersAsync(Guid bookId, int limit, CancellationToken ct = default);

    /// <summary>Блок «Популярное» (SQL, без ML).</summary>
    Task<RecommendationListDto> GetPopularAsync(int limit, CancellationToken ct = default);

    /// <summary>Блок «Подборка по жанрам» (content-based через «якорные» книги жанров).</summary>
    Task<RecommendationListDto> GetByPreferredGenresAsync(Guid userId, int limit, CancellationToken ct = default);

    /// <summary>Блок «Книги от похожих читателей» (User-CF).</summary>
    Task<RecommendationListDto> GetSimilarReadersAsync(Guid userId, int limit, CancellationToken ct = default);

    /// <summary>«Рекомендация дня» — одна книга от модели с лучшей метрикой, кэш сутки.</summary>
    Task<DailyRecommendationDto> GetDailyAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Health всех моделей.</summary>
    Task<RecommendationsHealthDto> GetHealthAsync(CancellationToken ct = default);

    /// <summary>Принудительное переобучение одной модели или всех.</summary>
    Task TriggerRetrainAsync(RecommendationModel? model, CancellationToken ct = default);
}
