using Brs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brs.Infrastructure.Persistence.Configurations;

/// <summary>EF-конфигурация для связки <see cref="BookAuthor"/>.</summary>
public class BookAuthorConfiguration : IEntityTypeConfiguration<BookAuthor>
{
    public void Configure(EntityTypeBuilder<BookAuthor> b)
    {
        b.ToTable("book_authors");
        // причина: составной PK гарантирует уникальность пары без отдельного индекса
        b.HasKey(x => new { x.BookId, x.AuthorId });

        b.HasOne(x => x.Book).WithMany(x => x.BookAuthors)
            .HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Author).WithMany(x => x.BookAuthors)
            .HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
    }
}
