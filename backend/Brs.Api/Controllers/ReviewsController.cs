using Brs.Api.Extensions;
using Brs.Application.DTOs.Books;
using Brs.Application.DTOs.Interactions;
using Brs.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brs.Api.Controllers;

/// <summary>Отзывы о книгах.</summary>
[ApiController]
[Route("api")]
public class ReviewsController : ControllerBase
{
    private readonly IInteractionService _interactions;

    public ReviewsController(IInteractionService interactions) => _interactions = interactions;

    /// <summary>GET /api/books/{bookId}/reviews — список отзывов книги с пагинацией.</summary>
    [HttpGet("books/{bookId:guid}/reviews")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReviewDto>>> GetBookReviews(
        Guid bookId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        => Ok(await _interactions.GetBookReviewsAsync(bookId, page, pageSize, ct));

    /// <summary>POST /api/books/{bookId}/reviews — создать отзыв.</summary>
    [HttpPost("books/{bookId:guid}/reviews")]
    [Authorize]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ReviewDto>> Create(
        Guid bookId, [FromBody] CreateReviewRequest request, CancellationToken ct)
    {
        var review = await _interactions.CreateReviewAsync(User.GetUserId(), bookId, request, ct);
        return StatusCode(StatusCodes.Status201Created, review);
    }

    /// <summary>PUT /api/reviews/{reviewId} — редактировать свой отзыв.</summary>
    [HttpPut("reviews/{reviewId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReviewDto>> Update(
        Guid reviewId, [FromBody] UpdateReviewRequest request, CancellationToken ct)
        => Ok(await _interactions.UpdateReviewAsync(User.GetUserId(), reviewId, request, ct));

    /// <summary>DELETE /api/reviews/{reviewId} — удалить отзыв (автор или Admin).</summary>
    [HttpDelete("reviews/{reviewId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid reviewId, CancellationToken ct)
    {
        await _interactions.DeleteReviewAsync(User.GetUserId(), reviewId, User.IsAdmin(), ct);
        return NoContent();
    }

    /// <summary>GET /api/users/me/reviews — отзывы текущего пользователя.</summary>
    [HttpGet("users/me/reviews")]
    [Authorize]
    [ProducesResponseType(typeof(PagedResult<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReviewDto>>> MyReviews(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
        => Ok(await _interactions.GetUserReviewsAsync(User.GetUserId(), page, pageSize, ct));
}
