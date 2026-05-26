using Brs.Api.Extensions;
using Brs.Application.DTOs.Interactions;
using Brs.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brs.Api.Controllers;

/// <summary>Оценки книг текущим пользователем.</summary>
[ApiController]
[Route("api/books/{bookId:guid}/ratings")]
[Authorize]
public class RatingsController : ControllerBase
{
    private readonly IInteractionService _interactions;

    public RatingsController(IInteractionService interactions) => _interactions = interactions;

    /// <summary>POST /api/books/{bookId}/ratings — создать или обновить оценку.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(RatingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RatingDto>> Rate(Guid bookId, [FromBody] RateBookRequest request, CancellationToken ct)
        => Ok(await _interactions.RateBookAsync(User.GetUserId(), bookId, request.Score, ct));

    /// <summary>DELETE /api/books/{bookId}/ratings — удалить свою оценку.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid bookId, CancellationToken ct)
    {
        await _interactions.DeleteRatingAsync(User.GetUserId(), bookId, ct);
        return NoContent();
    }

    /// <summary>GET /api/books/{bookId}/ratings/my — оценка текущего пользователя (404 если нет).</summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(RatingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RatingDto>> My(Guid bookId, CancellationToken ct)
    {
        var rating = await _interactions.GetUserRatingAsync(User.GetUserId(), bookId, ct);
        return rating is null ? NotFound() : Ok(rating);
    }
}
