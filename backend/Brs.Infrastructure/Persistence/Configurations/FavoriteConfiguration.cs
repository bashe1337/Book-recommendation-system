using Brs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brs.Infrastructure.Persistence.Configurations;

/// <summary>EF-конфигурация для <see cref="Favorite"/>.</summary>
public class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> b)
    {
        b.ToTable("favorites");
        b.HasKey(x => x.Id);
        b.Property(x => x.AddedAt).IsRequired();

        b.HasOne(x => x.User).WithMany(u => u.Favorites)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Book).WithMany()
            .HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);

        // причина: одна книга может быть в избранном у пользователя только один раз
        b.HasIndex(x => new { x.UserId, x.BookId }).IsUnique();
    }
}
