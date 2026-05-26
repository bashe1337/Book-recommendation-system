using Brs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brs.Infrastructure.Persistence.Configurations;

/// <summary>EF-конфигурация для <see cref="EditorialList"/>.</summary>
public class EditorialListConfiguration : IEntityTypeConfiguration<EditorialList>
{
    public void Configure(EntityTypeBuilder<EditorialList> b)
    {
        b.ToTable("editorial_lists");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).IsRequired().HasMaxLength(256);
        b.Property(x => x.Description).HasColumnType("text");
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.IsActive).IsRequired();

        // причина: явно настраиваем M2M-связь, чтобы зафиксировать имя таблицы-связки
        b.HasMany(x => x.Books).WithMany()
            .UsingEntity(j => j.ToTable("editorial_list_books"));
    }
}
