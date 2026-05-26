using Brs.Api.Extensions;
using Brs.Application.DTOs.Books;
using Brs.Application.DTOs.Interactions;
using Brs.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brs.Api.Controllers;

/// <summary>История просмотров книг текущим пользователем.</summary>
[ApiController]
[Route("api")]
[Authorize]
public class ViewHistoryController : ControllerBase
{
    private readonly IInteractionService _interactions;

    public ViewHistoryController(IInteractionService interactions) => _interactions = interactions;

    /// <summary>GET /api/users/me/history — история просмотров с пагинацией.</summary>
    [HttpGet("users/me/history")]
    [ProducesResponseType(typeof(PagedResult<ViewHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ViewHistoryDto>>> Get(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _interactions.GetUserViewHistoryAsync(User.GetUserId(), page, pageSize, ct));

    /// <summary>POST /api/books/{bookId}/view — зафиксировать просмотр (с дедупликацией).</summary>
    [HttpPost("books/{bookId:guid}/view")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Record(Guid bookId, CancellationToken ct)
    {
        // причина: всегда 204, даже если дедупликация пропустила запись —
        // фронту не нужно знать, была ли запись фактически создана
        await _interactions.RecordViewAsync(User.GetUserId(), bookId, ct);
        return NoContent();
    }

    /// <summary>DELETE /api/users/me/history — очистить всю историю просмотров.</summary>
    [HttpDelete("users/me/history")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        await _interactions.ClearViewHistoryAsync(User.GetUserId(), ct);
        return NoContent();
    }
}
