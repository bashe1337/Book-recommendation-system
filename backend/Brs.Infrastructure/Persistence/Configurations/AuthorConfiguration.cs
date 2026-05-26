using Brs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brs.Infrastructure.Persistence.Configurations;

/// <summary>EF-конфигурация для <see cref="Author"/>.</summary>
public class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> b)
    {
        b.ToTable("authors");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(256);
        b.Property(x => x.Bio).HasColumnType("text");
        b.HasIndex(x => x.Name);
    }
}
