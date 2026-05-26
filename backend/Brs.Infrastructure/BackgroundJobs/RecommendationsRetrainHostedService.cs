using Brs.Application.DTOs.Recommendations;
using Brs.Infrastructure.ExternalApis.Recommendations;
using Brs.Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Brs.Infrastructure.BackgroundJobs;

/// <summary>
/// Фоновый сервис: раз в RetrainIntervalHours переобучает все ML-модели.
/// Первый запуск — спустя 1 минуту после старта (даёт сервису подняться).
/// </summary>
public class RecommendationsRetrainHostedService : BackgroundService
{
    private static readonly RecommendationModel[] ModelsToRetrain =
    {
        // причина: content-based обучается при старте Python сам, ему отдельно retrain нужен
        // только если за сутки добавились новые книги — обновим вместе с остальными
        RecommendationModel.Content,
        RecommendationModel.Svd,
        RecommendationModel.Als,
        RecommendationModel.UserCf,
        RecommendationModel.ItemCf
    };

    private readonly RecommendationsTrainCoordinator _trainer;
    private readonly RecommendationsOptions _options;
    private readonly ILogger<RecommendationsRetrainHostedService> _logger;

    public RecommendationsRetrainHostedService(
        RecommendationsTrainCoordinator trainer,
        IOptions<RecommendationsOptions> options,
        ILogger<RecommendationsRetrainHostedService> logger)
    {
        _trainer = trainer;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // причина: дать Python-сервису подняться, а .NET закончить миграции/сидинг
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("[Retrain] начинаю суточное переобучение моделей");
            try
            {
                // причина: модели обучаются параллельно — ML-сервис однопоточный, но обращения
                // дробятся, ошибки в одной модели не валят остальные
                var tasks = ModelsToRetrain.Select(m => _trainer.EnsureTrainingAsync(m, stoppingToken)).ToArray();
                await Task.WhenAll(tasks);
                _logger.LogInformation("[Retrain] суточный цикл завершён");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[Retrain] ошибка в суточном цикле — продолжаю по расписанию");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(_options.RetrainIntervalHours), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
