using Brs.Application.Interfaces;
using Brs.Infrastructure.Auth;
using Brs.Infrastructure.BackgroundJobs;
using Brs.Infrastructure.ExternalApis.GoogleBooks;
using Brs.Infrastructure.ExternalApis.Recommendations;
using Brs.Infrastructure.Persistence;
using Brs.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Brs.Infrastructure;

/// <summary>Регистрация сервисов слоя Infrastructure в DI-контейнере.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Подключает <see cref="BrsDbContext"/>, JWT-опции и сервисы доменных сценариев.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection не задан");

        services.AddDbContext<BrsDbContext>(opt => opt.UseNpgsql(connectionString));

        // причина: биндим секцию "Jwt" — AuthService получит IOptions<JwtOptions>
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IInteractionService, InteractionService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IRecommendationService, RecommendationService>();

        // причина: типизированный HttpClient с базовым адресом из конфигурации —
        // HttpClientFactory управляет пулом соединений и их временем жизни
        services.Configure<GoogleBooksOptions>(config.GetSection(GoogleBooksOptions.SectionName));
        services.AddHttpClient<GoogleBooksClient>((sp, client) =>
        {
            var baseUrl = config[$"{GoogleBooksOptions.SectionName}:BaseUrl"]
                ?? "https://www.googleapis.com/books/v1";
            // причина: BaseAddress должен оканчиваться на '/', чтобы относительный путь добавлялся корректно
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        });

        // причина: типизированный клиент Python-сервиса рекомендаций. Таймаут берётся
        // из конфигурации — ML-запросы тяжелее, чем обычные HTTP-вызовы
        services.Configure<RecommendationsOptions>(config.GetSection(RecommendationsOptions.SectionName));
        services.AddHttpClient<RecommendationsClient>((sp, client) =>
        {
            var opt = sp.GetRequiredService<IOptions<RecommendationsOptions>>().Value;
            client.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);
        });

        // причина: координатор тренировок держит состояние «модель X сейчас обучается» —
        // singleton, чтобы оба фоновых сервиса и оркестратор делили одно состояние
        services.AddSingleton<RecommendationsTrainCoordinator>();

        // причина: суточный фоновый retrain
        services.AddHostedService<RecommendationsRetrainHostedService>();

        return services;
    }
}
