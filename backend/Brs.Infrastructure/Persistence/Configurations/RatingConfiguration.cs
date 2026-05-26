using Brs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brs.Infrastructure.Persistence.Configurations;

/// <summary>EF-конфигурация для <see cref="Rating"/>.</summary>
public class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> b)
    {
        b.ToTable("ratings", t =>
            // причина: запрещаем оценки вне диапазона 1–5 на уровне БД
            t.HasCheckConstraint("ck_ratings_score_range", "\"Score\" BETWEEN 1 AND 5"));

        b.HasKey(x => x.Id);
        b.Property(x => x.Score).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();

        b.HasOne(x => x.User).WithMany(u => u.Ratings)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Book).WithMany()
            .HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);

        // причина: один пользователь — одна оценка одной книги
        b.HasIndex(x => new { x.UserId, x.BookId }).IsUnique();
    }
}
