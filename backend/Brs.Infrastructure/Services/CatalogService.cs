using Brs.Application.DTOs.Books;
using Brs.Application.Exceptions;
using Brs.Application.Interfaces;
using Brs.Domain.Entities;
using Brs.Infrastructure.ExternalApis.GoogleBooks;
using Brs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brs.Infrastructure.Services;

/// <summary>Сервис каталога: книги, авторы, жанры, подборки, импорт из Google Books.</summary>
public class CatalogService : ICatalogService
{
    private const int MaxPageSize = 100;
    // причина: порог триграммной похожести подобран эмпирически — ниже даёт много мусора
    private const double TrigramThreshold = 0.2;

    private readonly BrsDbContext _db;
    private readonly GoogleBooksClient _googleBooks;

    public CatalogService(BrsDbContext db, GoogleBooksClient googleBooks)
    {
        _db = db;
        _googleBooks = googleBooks;
    }

    /// <inheritdoc/>
    public async Task<PagedResult<BookSummaryDto>> GetBooksAsync(BookFilterRequest filter, CancellationToken ct = default)
    {
        // причина: AsNoTracking — выборка только для чтения, не нужен change tracker
        var query = _db.Books
            .AsNoTracking()
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .Include(b => b.BookGenres).ThenInclude(bg => bg.Genre)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            // причина: полнотекстовый поиск по GIN-индексу (search_vector @@ plainto_tsquery)
            // ИЛИ триграммная похожесть title — ловит опечатки и подстроки.
            // Оба варианта используют индексы PostgreSQL, в отличие от LINQ Contains().
            query = query.Where(b =>
                b.SearchVector!.Matches(EF.Functions.PlainToTsQuery("russian", search))
                || EF.Functions.TrigramsSimilarity(b.Title, search) > TrigramThreshold);
        }

        if (filter.GenreIds is { Count: > 0 })
            query = query.Where(b => b.BookGenres.Any(bg => filter.GenreIds.Contains(bg.GenreId)));

        if (filter.AuthorId is { } authorId)
            query = query.Where(b => b.BookAuthors.Any(ba => ba.AuthorId == authorId));

        if (!string.IsNullOrWhiteSpace(filter.Language))
            query = query.Where(b => b.Language == filter.Language);

        if (filter.YearFrom is { } yearFrom)
            query = query.Where(b => b.PublishedYear >= yearFrom);

        if (filter.YearTo is { } yearTo)
            query = query.Where(b => b.PublishedYear <= yearTo);

        query = ApplySort(query, filter.SortBy, filter.SortDesc);

        var totalCount = await query.CountAsync(ct);

        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > MaxPageSize ? 20 : filter.PageSize;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BookSummaryDto
            {
                Id = b.Id,
                Title = b.Title,
                CoverUrl = b.CoverUrl,
                AvgRating = b.AvgRating,
                RatingCount = b.RatingCount,
                Authors = b.BookAuthors.Select(ba => ba.Author!.Name).ToList(),
                Genres = b.BookGenres.Select(bg => bg.Genre!.Name).ToList()
            })
            .ToListAsync(ct);

        return new PagedResult<BookSummaryDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc/>
    public async Task<BookDto> GetBookByIdAsync(Guid id, CancellationToken ct = default)
    {
        var book = await _db.Books
            .AsNoTracking()
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .Include(b => b.BookGenres).ThenInclude(bg => bg.Genre)
            .Include(b => b.BookTags).ThenInclude(bt => bt.Tag)
            .FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException($"Книга {id} не найдена.");

        return MapToDto(book);
    }

    /// <inheritdoc/>
    public async Task<BookDto> CreateBookAsync(CreateBookRequest request, CancellationToken ct = default)
    {
        var book = new Book
        {
            Title = request.Title,
            Description = request.Description,
            PublishedYear = request.PublishedYear,
            Language = request.Language,
            ISBN = request.ISBN,
            CoverUrl = request.CoverUrl,
            GoogleBooksId = request.GoogleBooksId
        };

        ApplyRelations(book, request.AuthorIds, request.GenreIds, request.TagIds);

        await _db.Books.AddAsync(book, ct);
        await _db.SaveChangesAsync(ct);

        // причина: перечитываем со связями, чтобы вернуть полностью заполненный DTO
        return await GetBookByIdAsync(book.Id, ct);
    }

    /// <inheritdoc/>
    public async Task<BookDto> UpdateBookAsync(Guid id, UpdateBookRequest request, CancellationToken ct = default)
    {
        var book = await _db.Books
            .Include(b => b.BookAuthors)
            .Include(b => b.BookGenres)
            .Include(b => b.BookTags)
            .FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException($"Книга {id} не найдена.");

        // причина: обновляем только переданные (не null) поля — частичное обновление
        if (request.Title is not null) book.Title = request.Title;
        if (request.Description is not null) book.Description = request.Description;
        if (request.PublishedYear is not null) book.PublishedYear = request.PublishedYear;
        if (request.Language is not null) book.Language = request.Language;
        if (request.ISBN is not null) book.ISBN = request.ISBN;
        if (request.CoverUrl is not null) book.CoverUrl = request.CoverUrl;
        if (request.GoogleBooksId is not null) book.GoogleBooksId = request.GoogleBooksId;

        if (request.AuthorIds is not null)
        {
            book.BookAuthors.Clear();
            foreach (var aid in request.AuthorIds.Distinct())
                book.BookAuthors.Add(new BookAuthor { BookId = book.Id, AuthorId = aid });
        }

        if (request.GenreIds is not null)
        {
            book.BookGenres.Clear();
            foreach (var gid in request.GenreIds.Distinct())
                book.BookGenres.Add(new BookGenre { BookId = book.Id, GenreId = gid });
        }

        if (request.TagIds is not null)
        {
            book.BookTags.Clear();
            foreach (var tid in request.TagIds.Distinct())
                book.BookTags.Add(new BookTag { BookId = book.Id, TagId = tid });
        }

        await _db.SaveChangesAsync(ct);
        return await GetBookByIdAsync(book.Id, ct);
    }

    /// <inheritdoc/>
    public async Task DeleteBookAsync(Guid id, CancellationToken ct = default)
    {
        var book = await _db.Books.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException($"Книга {id} не найдена.");

        _db.Books.Remove(book);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<List<AuthorDto>> GetAuthorsAsync(CancellationToken ct = default)
        => await _db.Authors.AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new AuthorDto { Id = a.Id, Name = a.Name, Bio = a.Bio })
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<AuthorDto> CreateAuthorAsync(CreateAuthorRequest request, CancellationToken ct = default)
    {
        var author = new Author { Name = request.Name, Bio = request.Bio };
        await _db.Authors.AddAsync(author, ct);
        await _db.SaveChangesAsync(ct);
        return new AuthorDto { Id = author.Id, Name = author.Name, Bio = author.Bio };
    }

    /// <inheritdoc/>
    public async Task<List<GenreDto>> GetGenresAsync(CancellationToken ct = default)
        => await _db.Genres.AsNoTracking()
            .OrderBy(g => g.Name)
            .Select(g => new GenreDto { Id = g.Id, Name = g.Name, Slug = g.Slug })
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<GenreDto> CreateGenreAsync(CreateGenreRequest request, CancellationToken ct = default)
    {
        if (await _db.Genres.AnyAsync(g => g.Slug == request.Slug, ct))
            throw new ValidationException($"Жанр со slug '{request.Slug}' уже существует.");

        var genre = new Genre { Name = request.Name, Slug = request.Slug };
        await _db.Genres.AddAsync(genre, ct);
        await _db.SaveChangesAsync(ct);
        return new GenreDto { Id = genre.Id, Name = genre.Name, Slug = genre.Slug };
    }

    /// <inheritdoc/>
    public async Task<List<EditorialListDto>> GetEditorialListsAsync(CancellationToken ct = default)
    {
        var lists = await _db.EditorialLists
            .AsNoTracking()
            .Where(l => l.IsActive)
            .Include(l => l.Books).ThenInclude(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .Include(l => l.Books).ThenInclude(b => b.BookGenres).ThenInclude(bg => bg.Genre)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        return lists.Select(MapListToDto).ToList();
    }

    /// <inheritdoc/>
    public async Task<EditorialListDto> CreateEditorialListAsync(
        string title, string description, List<Guid> bookIds, CancellationToken ct = default)
    {
        var books = await _db.Books
            .Where(b => bookIds.Contains(b.Id))
            .ToListAsync(ct);

        var list = new EditorialList
        {
            Title = title,
            Description = description,
            IsActive = true,
            Books = books
        };

        await _db.EditorialLists.AddAsync(list, ct);
        await _db.SaveChangesAsync(ct);

        // причина: перечитываем со связями для корректного DTO книг
        var saved = await _db.EditorialLists
            .AsNoTracking()
            .Include(l => l.Books).ThenInclude(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .Include(l => l.Books).ThenInclude(b => b.BookGenres).ThenInclude(bg => bg.Genre)
            .FirstAsync(l => l.Id == list.Id, ct);

        return MapListToDto(saved);
    }

    /// <inheritdoc/>
    public async Task<List<BookSummaryDto>> ImportFromGoogleBooksAsync(
        string query,
        GoogleBooksSearchField field = GoogleBooksSearchField.All,
        string? langRestrict = null,
        CancellationToken ct = default)
    {
        var volumes = await _googleBooks.SearchAsync(query, field, langRestrict, maxResults: 10, ct);
        var imported = new List<Book>();

        // причина: кешируем авторов в памяти, чтобы не плодить дубли и лишние запросы
        var authorCache = new Dictionary<string, Author>(StringComparer.OrdinalIgnoreCase);

        foreach (var volume in volumes)
        {
            if (string.IsNullOrWhiteSpace(volume.Id) || string.IsNullOrWhiteSpace(volume.VolumeInfo.Title))
                continue;

            // причина: дедупликация по GoogleBooksId — не импортируем уже существующее
            if (await _db.Books.AnyAsync(b => b.GoogleBooksId == volume.Id, ct))
                continue;

            var info = volume.VolumeInfo;
            var book = new Book
            {
                Title = info.Title!,
                Description = info.Description,
                PublishedYear = ParseYear(info.PublishedDate),
                Language = info.Language,
                ISBN = info.IndustryIdentifiers?
                    .FirstOrDefault(i => i.Type == "ISBN_13" || i.Type == "ISBN_10")?.Identifier,
                CoverUrl = info.ImageLinks?.Thumbnail ?? info.ImageLinks?.SmallThumbnail,
                GoogleBooksId = volume.Id
            };

            foreach (var authorName in info.Authors ?? Enumerable.Empty<string>())
            {
                var author = await ResolveAuthorAsync(authorName, authorCache, ct);
                book.BookAuthors.Add(new BookAuthor { Book = book, Author = author });
            }

            await _db.Books.AddAsync(book, ct);
            imported.Add(book);
        }

        await _db.SaveChangesAsync(ct);

        return imported.Select(b => new BookSummaryDto
        {
            Id = b.Id,
            Title = b.Title,
            CoverUrl = b.CoverUrl,
            AvgRating = b.AvgRating,
            RatingCount = b.RatingCount,
            Authors = b.BookAuthors.Select(ba => ba.Author!.Name).ToList(),
            Genres = new List<string>()
        }).ToList();
    }

    // ---- helpers ----

    /// <summary>Находит автора в кеше/БД или создаёт нового.</summary>
    private async Task<Author> ResolveAuthorAsync(
        string name, Dictionary<string, Author> cache, CancellationToken ct)
    {
        if (cache.TryGetValue(name, out var cached)) return cached;

        var existing = await _db.Authors.FirstOrDefaultAsync(a => a.Name == name, ct);
        if (existing is not null)
        {
            cache[name] = existing;
            return existing;
        }

        var created = new Author { Name = name };
        await _db.Authors.AddAsync(created, ct);
        cache[name] = created;
        return created;
    }

    /// <summary>Применяет сортировку по полю SortBy.</summary>
    private static IQueryable<Book> ApplySort(IQueryable<Book> query, string? sortBy, bool desc)
    {
        // причина: маппинг строкового ключа в выражение сортировки;
        // по умолчанию — рейтинг (наиболее релевантно для каталога)
        return (sortBy?.ToLowerInvariant()) switch
        {
            "date" => desc ? query.OrderByDescending(b => b.PublishedYear)
                           : query.OrderBy(b => b.PublishedYear),
            "popularity" => desc ? query.OrderByDescending(b => b.RatingCount)
                                 : query.OrderBy(b => b.RatingCount),
            _ => desc ? query.OrderByDescending(b => b.AvgRating)
                      : query.OrderBy(b => b.AvgRating)
        };
    }

    /// <summary>Привязывает авторов/жанры/теги к новой книге.</summary>
    private static void ApplyRelations(Book book, List<Guid> authorIds, List<Guid> genreIds, List<Guid> tagIds)
    {
        foreach (var aid in authorIds.Distinct())
            book.BookAuthors.Add(new BookAuthor { BookId = book.Id, AuthorId = aid });
        foreach (var gid in genreIds.Distinct())
            book.BookGenres.Add(new BookGenre { BookId = book.Id, GenreId = gid });
        foreach (var tid in tagIds.Distinct())
            book.BookTags.Add(new BookTag { BookId = book.Id, TagId = tid });
    }

    /// <summary>Извлекает год из строки даты Google Books ("2010", "2010-05-01").</summary>
    private static int? ParseYear(string? publishedDate)
    {
        if (string.IsNullOrWhiteSpace(publishedDate)) return null;
        var yearPart = publishedDate.Split('-')[0];
        return int.TryParse(yearPart, out var year) ? year : null;
    }

    private static BookDto MapToDto(Book b) => new()
    {
        Id = b.Id,
        Title = b.Title,
        Description = b.Description,
        PublishedYear = b.PublishedYear,
        Language = b.Language,
        ISBN = b.ISBN,
        CoverUrl = b.CoverUrl,
        AvgRating = b.AvgRating,
        RatingCount = b.RatingCount,
        Authors = b.BookAuthors.Select(ba => new AuthorDto
        {
            Id = ba.Author!.Id, Name = ba.Author.Name, Bio = ba.Author.Bio
        }).ToList(),
        Genres = b.BookGenres.Select(bg => new GenreDto
        {
            Id = bg.Genre!.Id, Name = bg.Genre.Name, Slug = bg.Genre.Slug
        }).ToList(),
        Tags = b.BookTags.Select(bt => new TagDto
        {
            Id = bt.Tag!.Id, Name = bt.Tag.Name
        }).ToList()
    };

    private static EditorialListDto MapListToDto(EditorialList l) => new()
    {
        Id = l.Id,
        Title = l.Title,
        Description = l.Description,
        CreatedAt = l.CreatedAt,
        IsActive = l.IsActive,
        Books = l.Books.Select(b => new BookSummaryDto
        {
            Id = b.Id,
            Title = b.Title,
            CoverUrl = b.CoverUrl,
            AvgRating = b.AvgRating,
            RatingCount = b.RatingCount,
            Authors = b.BookAuthors.Select(ba => ba.Author!.Name).ToList(),
            Genres = b.BookGenres.Select(bg => bg.Genre!.Name).ToList()
        }).ToList()
    };
}
