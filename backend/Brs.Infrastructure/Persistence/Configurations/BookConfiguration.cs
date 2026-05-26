using Brs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brs.Infrastructure.Persistence.Configurations;

/// <summary>EF-конфигурация для <see cref="Book"/>.</summary>
public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> b)
    {
        b.ToTable("books");
        b.HasKey(x => x.Id);

        b.Property(x => x.Title).IsRequired().HasMaxLength(512);
        b.Property(x => x.Description).HasColumnType("text");
        b.Property(x => x.Language).HasMaxLength(8);
        b.Property(x => x.ISBN).HasMaxLength(32);
        b.Property(x => x.GoogleBooksId).HasMaxLength(64);
        b.Property(x => x.CoverUrl).HasMaxLength(1024);

        b.HasIndex(x => x.ISBN);
        b.HasIndex(x => x.GoogleBooksId).IsUnique()
            .HasFilter("\"GoogleBooksId\" IS NOT NULL");

        // причина: SearchVector полностью управляется триггером в БД —
        // помечаем как computed/never inserted, чтобы EF не пытался писать значение.
        // HasComputedColumnSql(null) + ValueGeneratedOnAddOrUpdate сообщает EF:
        // «значение всегда приходит из БД, никогда не отправляй его в INSERT/UPDATE».
        b.Property(x => x.SearchVector)
            .HasColumnName("search_vector")
            .HasColumnType("tsvector")
            .ValueGeneratedOnAddOrUpdate()
            .Metadata.SetAfterSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore);

        b.Property(x => x.SearchVector).Metadata
            .SetBeforeSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore);
    }
}
