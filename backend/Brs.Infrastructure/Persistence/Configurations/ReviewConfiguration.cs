using Brs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brs.Infrastructure.Persistence.Configurations;

/// <summary>EF-конфигурация для <see cref="Review"/>.</summary>
public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> b)
    {
        b.ToTable("reviews");
        b.HasKey(x => x.Id);

        b.Property(x => x.Content).IsRequired().HasColumnType("text");
        b.Property(x => x.CreatedAt).IsRequired();

        b.HasOne(x => x.User).WithMany(u => u.Reviews)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Book).WithMany()
            .HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);

        // причина: типичный запрос — «все отзывы по книге, отсортированные по дате»
        b.HasIndex(x => new { x.BookId, x.CreatedAt });
    }
}
