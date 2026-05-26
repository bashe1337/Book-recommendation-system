using Brs.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Brs.Infrastructure.Persistence;

/// <summary>EF Core контекст основной БД сервиса рекомендаций.</summary>
public class BrsDbContext : DbContext
{
    public BrsDbContext(DbContextOptions<BrsDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<ViewHistory> ViewHistory => Set<ViewHistory>();
    public DbSet<EditorialList> EditorialLists => Set<EditorialList>();
    public DbSet<BookAuthor> BookAuthors => Set<BookAuthor>();
    public DbSet<BookGenre> BookGenres => Set<BookGenre>();
    public DbSet<BookTag> BookTags => Set<BookTag>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // причина: применяем все IEntityTypeConfiguration<T> разом —
        // настройки каждой сущности изолированы в своём файле
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BrsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
