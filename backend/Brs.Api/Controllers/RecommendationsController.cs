using Brs.Api.Extensions;
using Brs.Application.DTOs.Recommendations;
using Brs.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brs.Api.Controllers;

/// <summary>Рекомендации: тонкий контроллер над оркестратором.</summary>
[ApiController]
[Route("api")]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommendationService _service;

    public RecommendationsController(IRecommendationService service) => _service = service;

    /// <summary>GET /api/recommendations/for-you — блок «Рекомендовано вам».</summary>
    [HttpGet("recommendations/for-you")]
    [Authorize]
    [ProducesResponseType(typeof(RecommendationListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecommendationListDto>> ForYou(
        [FromQuery] int limit = 10, CancellationToken ct = default)
        => Ok(await _service.GetForYouAsync(User.GetUserId(), limit, ct));

    /// <summary>GET /api/recommendations/may-like — блок «Вам может понравиться» (ALS).</summary>
    [HttpGet("recommendations/may-like")]
    [Authorize]
    [ProducesResponseType(typeof(RecommendationListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecommendationListDto>> MayLike(
        [FromQuery] int limit = 10, CancellationToken ct = default)
        => Ok(await _service.GetMayLikeAsync(User.GetUserId(), limit, ct));

    /// <summary>GET /api/recommendations/based-on-my-ratings — блок «Похожее на оценённые книги».</summary>
    [HttpGet("recommendations/based-on-my-ratings")]
    [Authorize]
    [ProducesResponseType(typeof(BasedOnMyRatingsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BasedOnMyRatingsResponseDto>> BasedOnMyRatings(
        [FromQuery] int sources = 10,
        [FromQuery] int perSource = 10,
        CancellationToken ct = default)
        => Ok(await _service.GetBasedOnMyRatingsAsync(User.GetUserId(), sources, perSource, ct));

    /// <summary>GET /api/recommendations/popular — блок «Популярное» (без ML).</summary>
    [HttpGet("recommendations/popular")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RecommendationListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecommendationListDto>> Popular(
        [FromQuery] int limit = 10, CancellationToken ct = default)
        => Ok(await _service.GetPopularAsync(limit, ct));

    /// <summary>GET /api/recommendations/by-preferred-genres — блок «Подборка по жанрам».</summary>
    [HttpGet("recommendations/by-preferred-genres")]
    [Authorize]
    [ProducesResponseType(typeof(RecommendationListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecommendationListDto>> ByPreferredGenres(
        [FromQuery] int limit = 10, CancellationToken ct = default)
        => Ok(await _service.GetByPreferredGenresAsync(User.GetUserId(), limit, ct));

    /// <summary>GET /api/recommendations/similar-readers — блок «Книги от похожих читателей» (User-CF).</summary>
    [HttpGet("recommendations/similar-readers")]
    [Authorize]
    [ProducesResponseType(typeof(RecommendationListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecommendationListDto>> SimilarReaders(
        [FromQuery] int limit = 10, CancellationToken ct = default)
        => Ok(await _service.GetSimilarReadersAsync(User.GetUserId(), limit, ct));

    /// <summary>GET /api/recommendations/daily — «Рекомендация дня» (одна книга, кэш 24ч).</summary>
    [HttpGet("recommendations/daily")]
    [Authorize]
    [ProducesResponseType(typeof(DailyRecommendationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DailyRecommendationDto>> Daily(CancellationToken ct)
        => Ok(await _service.GetDailyAsync(User.GetUserId(), ct));

    /// <summary>GET /api/books/{bookId}/recommendations — блок «Рекомендуем похожее» (страница поиска).</summary>
    [HttpGet("books/{bookId:guid}/recommendations")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RecommendationListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecommendationListDto>> SimilarToBook(
        Guid bookId, [FromQuery] int limit = 10, CancellationToken ct = default)
        => Ok(await _service.GetSimilarToBookAsync(bookId, limit, ct));

    /// <summary>GET /api/books/{bookId}/co-readers — блок «С этой книгой читают» (item-CF).</summary>
    [HttpGet("books/{bookId:guid}/co-readers")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RecommendationListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecommendationListDto>> CoReaders(
        Guid bookId, [FromQuery] int limit = 10, CancellationToken ct = default)
        => Ok(await _service.GetCoReadersAsync(bookId, limit, ct));

    /// <summary>GET /api/recommendations/health — состояние ML-сервиса и моделей.</summary>
    [HttpGet("recommendations/health")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(RecommendationsHealthDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecommendationsHealthDto>> Health(CancellationToken ct)
        => Ok(await _service.GetHealthAsync(ct));

    /// <summary>POST /api/recommendations/retrain?model=... — принудительный retrain (без model — всех).</summary>
    [HttpPost("recommendations/retrain")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Retrain([FromQuery] RecommendationModel? model, CancellationToken ct)
    {
        await _service.TriggerRetrainAsync(model, ct);
        return Accepted();
    }
}
