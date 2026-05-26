using System.Reflection;
using System.Text.Json;
using Brs.Domain.Entities;
using Brs.Infrastructure.Persistence.SeedData;
using Microsoft.EntityFrameworkCore;

namespace Brs.Infrastructure.Persistence;

/// <summary>
/// Идемпотентный сидер: загружает каталог из books.json (исходник — frontend/mocks/library.js),
/// добавляет тестовых пользователей с паттерн-оценками для демонстрации рекомендаций.
/// Безопасно вызывать многократно — повторяющиеся данные не дублируются.
/// </summary>
public static class DataSeeder
{
    // причина: один пароль на всех тестовых пользователей упрощает демонстрацию комиссии
    private const string TestUserPassword = "Reader123!";
    private const string TestEmailDomain = "@brs.local";
    private const int TestUsersCount = 50;
    private const int Seed = 42; // причина: детерминированный random — стабильные данные между запусками

    /// <summary>
    /// Полная семинизация: каталог книг + редакционная подборка + 50 тестовых пользователей с оценками.
    /// </summary>
    public static async Task SeedAsync(BrsDbContext db, CancellationToken ct = default)
    {
        var (genreByName, _, _) = await SeedCatalogAsync(db, ct);
        await SeedEditorialListAsync(db, ct);
        await SeedTestUsersAsync(db, genreByName, ct);
        await RecomputeBookAggregatesAsync(db, ct);
    }

    // =====================================================================
    // Каталог: жанры, авторы, теги, книги
    // =====================================================================

    private static async Task<(
        Dictionary<string, Genre> genres,
        Dictionary<string, Author> authors,
        Dictionary<string, Tag> tags)>
        SeedCatalogAsync(BrsDbContext db, CancellationToken ct)
    {
        var seeds = LoadBookSeeds();

        // причина: уникальные имена сущностей из исходного набора — основа для идемпотентного UPSERT'а
        var genreNames = seeds.SelectMany(b => b.Genres).Distinct().ToList();
        var authorNames = seeds.Select(b => b.Author).Distinct().ToList();
        var tagNames = seeds.SelectMany(b => b.Tags).Distinct().ToList();

        // Жанры
        var existingGenres = await db.Genres.ToDictionaryAsync(g => g.Name, ct);
        // причина: slug уникален в БД — собираем существующие, чтобы при коллизии
        // (например, остатки старого сидера) сгенерировать суффикс и не падать SaveChanges'ом
        var existingGenreSlugs = new HashSet<string>(
            existingGenres.Values.Select(g => g.Slug), StringComparer.OrdinalIgnoreCase);
        foreach (var name in genreNames)
        {
            if (existingGenres.ContainsKey(name)) continue;
            var slug = SlugHelper.SlugForGenre(name);
            var uniqueSlug = slug;
            int suffix = 2;
            while (!existingGenreSlugs.Add(uniqueSlug))
                uniqueSlug = $"{slug}-{suffix++}";
            var genre = new Genre { Name = name, Slug = uniqueSlug };
            db.Genres.Add(genre);
            existingGenres[name] = genre;
        }

        // Авторы
        var existingAuthors = await db.Authors.ToDictionaryAsync(a => a.Name, ct);
        foreach (var name in authorNames)
        {
            if (existingAuthors.ContainsKey(name)) continue;
            var author = new Author { Name = name, Bio = $"Автор: {name}" };
            db.Authors.Add(author);
            existingAuthors[name] = author;
        }

        // Теги
        var existingTags = await db.Tags.ToDictionaryAsync(t => t.Name, ct);
        foreach (var name in tagNames)
        {
            if (existingTags.ContainsKey(name)) continue;
            var tag = new Tag { Name = name };
            db.Tags.Add(tag);
            existingTags[name] = tag;
        }

        await db.SaveChangesAsync(ct);

        // Книги: ключ идемпотентности — пара (Title, AuthorName). Этого достаточно для нашего датасета.
        var existingBooks = await db.Books
            .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
            .ToListAsync(ct);
        var bookKey = new HashSet<string>(existingBooks.Select(b => BookKey(b.Title, b.BookAuthors.FirstOrDefault()?.Author?.Name ?? "")));

        foreach (var s in seeds)
        {
            if (bookKey.Contains(BookKey(s.Title, s.Author))) continue;

            var book = new Book
            {
                Title = s.Title,
                Description = s.Description,
                PublishedYear = s.Year,
                Language = SlugHelper.LanguageCode(s.Lang),
                // причина: ISBN детерминированно генерируем по mockId — поле в БД индексируется,
                // но не уникально, так что коллизии после повторных сидов не страшны
                ISBN = GenerateIsbn(s.MockId),
                // причина: обложек нет — на фронте генератор плейсхолдеров, см. BookCoverPlaceholder.vue
                CoverUrl = null
            };

            foreach (var g in s.Genres.Distinct())
                book.BookGenres.Add(new BookGenre { Book = book, Genre = existingGenres[g] });
            foreach (var t in s.Tags.Distinct())
                book.BookTags.Add(new BookTag { Book = book, Tag = existingTags[t] });
            book.BookAuthors.Add(new BookAuthor { Book = book, Author = existingAuthors[s.Author] });

            db.Books.Add(book);
            bookKey.Add(BookKey(s.Title, s.Author));
        }

        await db.SaveChangesAsync(ct);
        return (existingGenres, existingAuthors, existingTags);
    }

    private static async Task SeedEditorialListAsync(BrsDbContext db, CancellationToken ct)
    {
        const string title = "Стартовая подборка классики и фантастики";
        if (await db.EditorialLists.AnyAsync(l => l.Title == title, ct)) return;

        // причина: для подборки берём первые 8 классических/фантастических книг — то, что заведомо есть
        var picks = await db.Books.AsNoTracking()
            .Where(b => b.BookGenres.Any(bg => bg.Genre!.Name == "Классика" || bg.Genre.Name == "Фантастика"))
            .OrderBy(b => b.Title)
            .Take(8)
            .Select(b => b.Id)
            .ToListAsync(ct);
        if (picks.Count == 0) return;

        var list = new EditorialList
        {
            Title = title,
            Description = "Классические русские и зарубежные книги вместе с лучшей фантастикой XX века.",
            IsActive = true,
            Books = await db.Books.Where(b => picks.Contains(b.Id)).ToListAsync(ct)
        };
        db.EditorialLists.Add(list);
        await db.SaveChangesAsync(ct);
    }

    // =====================================================================
    // Тестовые пользователи и их оценки с паттернами по жанрам
    // =====================================================================

    private static async Task SeedTestUsersAsync(
        BrsDbContext db, Dictionary<string, Genre> genreByName, CancellationToken ct)
    {
        var existingUsers = await db.Users
            .Where(u => u.Email.EndsWith(TestEmailDomain))
            .Select(u => u.Email).ToListAsync(ct);
        var existingSet = existingUsers.ToHashSet();

        var allBooks = await db.Books
            .Include(b => b.BookGenres).ThenInclude(bg => bg.Genre)
            .ToListAsync(ct);
        if (allBooks.Count == 0) return;

        // причина: индекс книг по жанру — каждому шаблонному юзеру быстро формируем пул
        var booksByGenre = allBooks
            .SelectMany(b => b.BookGenres.Select(bg => new { Genre = bg.Genre!.Name, Book = b }))
            .GroupBy(x => x.Genre)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Book).ToList());

        // причина: пароль хешим один раз — BCrypt дорогой, 50 раз делать незачем
        var sharedPasswordHash = BCrypt.Net.BCrypt.HashPassword(TestUserPassword);

        var rng = new Random(Seed);

        // Профили — три кластера, как в реальной аудитории:
        //   - 10 «холодных» с 3-5 оценками (для проверки fallback-логики content-based)
        //   - 25 «средних» с 8-20 оценками (основная масса)
        //   - 15 «активных» с 20-40 оценками (даёт SVD/CF плотность для обучения)
        var profiles = BuildProfileDistribution(rng);

        var genreNames = genreByName.Keys.ToList();

        for (int i = 1; i <= TestUsersCount; i++)
        {
            var email = $"reader{i}{TestEmailDomain}";
            if (existingSet.Contains(email)) continue;

            // причина: каждому пользователю — 1-3 предпочитаемых жанра, попадающих в его «вселенную»
            var preferredCount = rng.Next(1, 4);
            var preferredGenreNames = PickDistinct(genreNames, preferredCount, rng);
            var preferredGenreIds = preferredGenreNames
                .Select(n => genreByName[n].Id)
                .ToList();

            var user = new User
            {
                Email = email,
                Username = $"Reader{i:00}",
                PasswordHash = sharedPasswordHash,
                Role = UserRole.User,
                RegisteredAt = DateTime.UtcNow.AddDays(-rng.Next(1, 365)),
                PreferredGenreIds = preferredGenreIds
            };
            db.Users.Add(user);

            // Соберём пул книг и сгенерим оценки
            var profile = profiles[(i - 1) % profiles.Count];
            var ratingsForUser = GenerateUserRatings(
                user, profile, preferredGenreNames, booksByGenre, allBooks, rng);
            foreach (var r in ratingsForUser)
                db.Ratings.Add(r);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Распределение профилей: cold/medium/power — для разных паттернов оценок.</summary>
    private static List<RatingProfile> BuildProfileDistribution(Random rng)
    {
        var list = new List<RatingProfile>();
        // 10 «холодных»
        for (int i = 0; i < 10; i++) list.Add(new RatingProfile(MinRatings: 3, MaxRatings: 5));
        // 25 «средних»
        for (int i = 0; i < 25; i++) list.Add(new RatingProfile(MinRatings: 8, MaxRatings: 20));
        // 15 «активных»
        for (int i = 0; i < 15; i++) list.Add(new RatingProfile(MinRatings: 20, MaxRatings: 40));
        // причина: тасуем, чтобы reader1..reader50 не разделялись блоками — они шли вперемешку
        return Shuffle(list, rng);
    }

    /// <summary>
    /// Генерирует оценки одного пользователя по паттерну:
    ///   - ~75% книг — из предпочитаемых жанров, оценки сдвинуты к высоким (mean 4.3)
    ///   - ~25% — из других жанров, оценки спокойнее (mean 3.0, шире разброс)
    /// </summary>
    private static List<Rating> GenerateUserRatings(
        User user,
        RatingProfile profile,
        List<string> preferredGenres,
        Dictionary<string, List<Book>> booksByGenre,
        List<Book> allBooks,
        Random rng)
    {
        var totalCount = rng.Next(profile.MinRatings, profile.MaxRatings + 1);
        var preferredCount = (int)Math.Round(totalCount * 0.75);
        var otherCount = Math.Max(0, totalCount - preferredCount);

        // причина: пул из предпочитаемых жанров — без дублей, чтобы не оценивать одну книгу дважды
        var preferredPool = preferredGenres
            .SelectMany(g => booksByGenre.GetValueOrDefault(g, new List<Book>()))
            .Distinct()
            .ToList();
        var preferredPick = SampleDistinct(preferredPool, preferredCount, rng);

        // Пул прочих жанров: всё кроме уже выбранного
        var pickedIds = preferredPick.Select(b => b.Id).ToHashSet();
        var otherPool = allBooks.Where(b => !pickedIds.Contains(b.Id)).ToList();
        var otherPick = SampleDistinct(otherPool, otherCount, rng);

        var ratings = new List<Rating>();
        foreach (var book in preferredPick)
            ratings.Add(MakeRating(user, book, mean: 4.3, std: 0.7, rng));
        foreach (var book in otherPick)
            ratings.Add(MakeRating(user, book, mean: 3.0, std: 1.1, rng));

        return ratings;
    }

    private static Rating MakeRating(User user, Book book, double mean, double std, Random rng)
    {
        // причина: нормальное распределение → клип в 1..5, округление до целого; реалистичная форма
        var value = (int)Math.Round(SampleNormal(rng, mean, std));
        var score = Math.Clamp(value, 1, 5);
        return new Rating
        {
            UserId = user.Id,
            BookId = book.Id,
            User = user,
            Book = book,
            Score = score,
            CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(1, 200))
        };
    }

    // =====================================================================
    // Финальный пересчёт денормализованных AvgRating/RatingCount
    // =====================================================================

    private static async Task RecomputeBookAggregatesAsync(BrsDbContext db, CancellationToken ct)
    {
        // причина: оценки добавлялись массово в обход InteractionService, поэтому
        // делаем один SQL-проход и обновляем агрегаты разом — без N+1 на каждую книгу
        var aggregates = await db.Ratings.AsNoTracking()
            .GroupBy(r => r.BookId)
            .Select(g => new
            {
                BookId = g.Key,
                Avg = g.Average(r => (double)r.Score),
                Count = g.Count()
            })
            .ToListAsync(ct);

        if (aggregates.Count == 0) return;

        var aggByBook = aggregates.ToDictionary(a => a.BookId);
        var books = await db.Books.Where(b => aggByBook.Keys.Contains(b.Id)).ToListAsync(ct);
        foreach (var book in books)
        {
            var a = aggByBook[book.Id];
            book.AvgRating = a.Avg;
            book.RatingCount = a.Count;
        }
        await db.SaveChangesAsync(ct);
    }

    // =====================================================================
    // Утилиты
    // =====================================================================

    private static List<BookSeedDto> LoadBookSeeds()
    {
        // причина: JSON лежит рядом с DLL после билда (см. csproj CopyToOutputDirectory)
        var dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppContext.BaseDirectory;
        var path = Path.Combine(dllDir, "Persistence", "SeedData", "books.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Не найден файл сидов каталога: {path}");

        var json = File.ReadAllText(path);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<BookSeedDto>>(json, opts) ?? new List<BookSeedDto>();
    }

    private static string BookKey(string title, string author)
        => $"{title.Trim().ToLowerInvariant()}|{author.Trim().ToLowerInvariant()}";

    private static string GenerateIsbn(int mockId)
    {
        // причина: формат как во фронте (mocks/bookMocks.js generateIsbn) — для удобства сверки
        var a = (900 + (mockId % 100)).ToString("D3");
        var b = (1000 + mockId * 13).ToString();
        b = b.Substring(Math.Max(0, b.Length - 4));
        var c = (mockId * 7919).ToString();
        c = c.Substring(Math.Max(0, c.Length - 5)).PadLeft(5, '0');
        return $"978-5-{a}-{b}-{c[..1]}";
    }

    private static List<T> PickDistinct<T>(IList<T> source, int count, Random rng)
    {
        if (count >= source.Count) return source.ToList();
        var indices = new HashSet<int>();
        while (indices.Count < count) indices.Add(rng.Next(source.Count));
        return indices.Select(i => source[i]).ToList();
    }

    private static List<T> SampleDistinct<T>(IList<T> source, int count, Random rng)
    {
        if (count <= 0 || source.Count == 0) return new List<T>();
        if (count >= source.Count) return Shuffle(source.ToList(), rng);
        return Shuffle(source.ToList(), rng).Take(count).ToList();
    }

    private static List<T> Shuffle<T>(List<T> list, Random rng)
    {
        // причина: Фишер-Йетс — O(n), гарантирует равномерность
        for (int i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    private static double SampleNormal(Random rng, double mean, double std)
    {
        // причина: Box–Muller — простой способ получить нормально распределённое число из Uniform
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return mean + std * z;
    }

    /// <summary>Параметры одного типового профиля.</summary>
    private record RatingProfile(int MinRatings, int MaxRatings);
}
