using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Brs.Infrastructure.Persistence;

/// <summary>
/// Design-time фабрика для dotnet-ef tooling. Позволяет выполнять
/// `dotnet ef migrations add ...` напрямую из проекта Infrastructure
/// без запуска веб-приложения.
/// </summary>
public class BrsDbContextFactory : IDesignTimeDbContextFactory<BrsDbContext>
{
    public BrsDbContext CreateDbContext(string[] args)
    {
        // причина: ищем appsettings.json в соседнем проекте Api,
        // чтобы строка подключения была единственным источником истины
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Brs.Api");
        var config = new ConfigurationBuilder()
            .SetBasePath(Path.GetFullPath(basePath))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection не задан");

        var options = new DbContextOptionsBuilder<BrsDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new BrsDbContext(options);
    }
}
