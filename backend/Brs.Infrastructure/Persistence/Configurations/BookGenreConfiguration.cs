using Brs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brs.Infrastructure.Persistence.Configurations;

/// <summary>EF-конфигурация для связки <see cref="BookGenre"/>.</summary>
public class BookGenreConfiguration : IEntityTypeConfiguration<BookGenre>
{
    public void Configure(EntityTypeBuilder<BookGenre> b)
    {
        b.ToTable("book_genres");
        b.HasKey(x => new { x.BookId, x.GenreId });

        b.HasOne(x => x.Book).WithMany(x => x.BookGenres)
            .HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Genre).WithMany(x => x.BookGenres)
            .HasForeignKey(x => x.GenreId).OnDelete(DeleteBehavior.Cascade);
    }
}
