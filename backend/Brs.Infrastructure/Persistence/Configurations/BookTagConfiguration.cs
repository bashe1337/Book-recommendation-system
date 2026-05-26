using Brs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brs.Infrastructure.Persistence.Configurations;

/// <summary>EF-конфигурация для связки <see cref="BookTag"/>.</summary>
public class BookTagConfiguration : IEntityTypeConfiguration<BookTag>
{
    public void Configure(EntityTypeBuilder<BookTag> b)
    {
        b.ToTable("book_tags");
        b.HasKey(x => new { x.BookId, x.TagId });

        b.HasOne(x => x.Book).WithMany(x => x.BookTags)
            .HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Tag).WithMany(x => x.BookTags)
            .HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
    }
}
