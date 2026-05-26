using Brs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brs.Infrastructure.Persistence.Configurations;

/// <summary>EF-конфигурация для <see cref="ViewHistory"/>.</summary>
public class ViewHistoryConfiguration : IEntityTypeConfiguration<ViewHistory>
{
    public void Configure(EntityTypeBuilder<ViewHistory> b)
    {
        b.ToTable("view_history");
        b.HasKey(x => x.Id);
        b.Property(x => x.ViewedAt).IsRequired();

        b.HasOne(x => x.User).WithMany(u => u.ViewHistory)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Book).WithMany()
            .HasForeignKey(x => x.BookId).OnDelete(DeleteBehavior.Cascade);

        // причина: для коллаборативной фильтрации нужно быстро находить
        // все просмотры пары (пользователь, книга) — не делаем уникальным,
        // так как один пользователь может смотреть одну книгу многократно
        b.HasIndex(x => new { x.UserId, x.BookId });
    }
}
