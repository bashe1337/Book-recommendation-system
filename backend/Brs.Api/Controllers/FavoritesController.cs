using Brs.Api.Extensions;
using Brs.Application.DTOs.Books;
using Brs.Application.DTOs.Interactions;
using Brs.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brs.Api.Controllers;

/// <summary>Избранные книги текущего пользователя.</summary>
[ApiController]
[Route("api/users/me/favorites")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly IInteractionService _interactions;

    public FavoritesController(IInteractionService interactions) => _interactions = interactions;

    /// <summary>GET /api/users/me/favorites — список избранного с пагинацией.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<FavoriteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<FavoriteDto>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _interactions.GetUserFavoritesAsync(User.GetUserId(), page, pageSize, ct));

    /// <summary>POST /api/users/me/favorites/{bookId} — добавить книгу в избранное.</summary>
    [HttpPost("{bookId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Add(Guid bookId, CancellationToken ct)
    {
        await _interactions.AddToFavoritesAsync(User.GetUserId(), bookId, ct);
        return NoContent();
    }

    /// <summary>DELETE /api/users/me/favorites/{bookId} — убрать книгу из избранного.</summary>
    [HttpDelete("{bookId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(Guid bookId, CancellationToken ct)
    {
        await _interactions.RemoveFromFavoritesAsync(User.GetUserId(), bookId, ct);
        return NoContent();
    }

    /// <summary>GET /api/users/me/favorites/{bookId}/check — в избранном ли книга.</summary>
    [HttpGet("{bookId:guid}/check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Check(Guid bookId, CancellationToken ct)
    {
        var isFavorited = await _interactions.IsBookFavoritedAsync(User.GetUserId(), bookId, ct);
        return Ok(new { isFavorited });
    }
}
