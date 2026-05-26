using Brs.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brs.Infrastructure.Persistence.Configurations;

/// <summary>EF-конфигурация для <see cref="User"/>.</summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);

        b.Property(x => x.Email).IsRequired().HasMaxLength(256);
        b.Property(x => x.PasswordHash).IsRequired();
        b.Property(x => x.Username).IsRequired().HasMaxLength(64);
        b.Property(x => x.RegisteredAt).IsRequired();
        b.Property(x => x.Role).HasConversion<int>();

        // причина: email — естественный логин, уникальность гарантируем на уровне БД
        b.HasIndex(x => x.Email).IsUnique();
        b.HasIndex(x => x.Username).IsUnique();

        // причина: Npgsql умеет мапить List<Guid> в нативный массив uuid[]
        b.Property(x => x.PreferredGenreIds).HasColumnType("uuid[]");
    }
}
