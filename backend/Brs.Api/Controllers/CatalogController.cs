using Brs.Application.DTOs.Books;
using Brs.Application.DTOs.Interactions;
using Brs.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brs.Api.Controllers;

/// <summary>Каталог: книги, авторы, жанры, редакционные подборки и импорт.</summary>
[ApiController]
[Route("api")]
public class CatalogController : ControllerBase
{
    private readonly ICatalogService _catalog;
    private readonly IInteractionService _interactions;

    public CatalogController(ICatalogService catalog, IInteractionService interactions)
    {
        _catalog = catalog;
        _interactions = interactions;
    }

    // ---- Books ----

    /// <summary>GET /api/books — список книг с фильтрацией, сортировкой и пагинацией.</summary>
    [HttpGet("books")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<BookSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<BookSummaryDto>>> GetBooks(
        [FromQuery] BookFilterRequest filter, CancellationToken ct)
        => Ok(await _catalog.GetBooksAsync(filter, ct));

    /// <summary>GET /api/books/{id} — карточка книги со всеми связями.</summary>
    [HttpGet("books/{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BookDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BookDto>> GetBook(Guid id, CancellationToken ct)
        => Ok(await _catalog.GetBookByIdAsync(id, ct));

    /// <summary>POST /api/books — создать книгу (только Admin).</summary>
    [HttpPost("books")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(BookDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<BookDto>> CreateBook([FromBody] CreateBookRequest request, CancellationToken ct)
    {
        var book = await _catalog.CreateBookAsync(request, ct);
        return CreatedAtAction(nameof(GetBook), new { id = book.Id }, book);
    }

    /// <summary>PUT /api/books/{id} — обновить книгу (только Admin).</summary>
    [HttpPut("books/{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(BookDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BookDto>> UpdateBook(Guid id, [FromBody] UpdateBookRequest request, CancellationToken ct)
        => Ok(await _catalog.UpdateBookAsync(id, request, ct));

    /// <summary>DELETE /api/books/{id} — удалить книгу (только Admin).</summary>
    [HttpDelete("books/{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteBook(Guid id, CancellationToken ct)
    {
        await _catalog.DeleteBookAsync(id, ct);
        return NoContent();
    }

    /// <summary>GET /api/books/{bookId}/stats — агрегированная статистика книги.</summary>
    [HttpGet("books/{bookId:guid}/stats")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BookStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BookStatsDto>> GetBookStats(Guid bookId, CancellationToken ct)
        => Ok(await _interactions.GetBookStatsAsync(bookId, ct));

    /// <summary>POST /api/books/import — импорт книг из Google Books (только Admin).</summary>
    [HttpPost("books/import")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(List<BookSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BookSummaryDto>>> Import(
        [FromBody] GoogleBooksImportRequest request, CancellationToken ct)
        => Ok(await _catalog.ImportFromGoogleBooksAsync(
            request.Query, request.SearchField, request.LangRestrict, ct));

    // ---- Authors ----

    /// <summary>GET /api/authors — список авторов.</summary>
    [HttpGet("authors")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<AuthorDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AuthorDto>>> GetAuthors(CancellationToken ct)
        => Ok(await _catalog.GetAuthorsAsync(ct));

    /// <summary>POST /api/authors — создать автора (только Admin).</summary>
    [HttpPost("authors")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(AuthorDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<AuthorDto>> CreateAuthor([FromBody] CreateAuthorRequest request, CancellationToken ct)
    {
        var author = await _catalog.CreateAuthorAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, author);
    }

    // ---- Genres ----

    /// <summary>GET /api/genres — список жанров.</summary>
    [HttpGet("genres")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<GenreDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GenreDto>>> GetGenres(CancellationToken ct)
        => Ok(await _catalog.GetGenresAsync(ct));

    /// <summary>POST /api/genres — создать жанр (только Admin).</summary>
    [HttpPost("genres")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(GenreDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<GenreDto>> CreateGenre([FromBody] CreateGenreRequest request, CancellationToken ct)
    {
        var genre = await _catalog.CreateGenreAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, genre);
    }

    // ---- Editorial lists ----

    /// <summary>GET /api/editorial-lists — активные редакционные подборки.</summary>
    [HttpGet("editorial-lists")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<EditorialListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EditorialListDto>>> GetEditorialLists(CancellationToken ct)
        => Ok(await _catalog.GetEditorialListsAsync(ct));

    /// <summary>POST /api/editorial-lists — создать подборку (только Admin).</summary>
    [HttpPost("editorial-lists")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(EditorialListDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<EditorialListDto>> CreateEditorialList(
        [FromBody] CreateEditorialListRequest request, CancellationToken ct)
    {
        var list = await _catalog.CreateEditorialListAsync(
            request.Title, request.Description ?? string.Empty, request.BookIds, ct);
        return StatusCode(StatusCodes.Status201Created, list);
    }
}
