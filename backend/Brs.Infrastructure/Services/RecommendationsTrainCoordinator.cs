using System.Collections.Concurrent;
using Brs.Application.DTOs.Recommendations;
using Brs.Infrastructure.ExternalApis.Recommendations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Brs.Infrastructure.Services;

/// <summary>
/// Координатор тренировок: не даёт запускать одно и то же обучение параллельно
/// и фиксирует «когда последний раз модель была обучена» (для суточного цикла).
/// Хранится в DI как singleton — состояние общее на процесс.
/// </summary>
public class RecommendationsTrainCoordinator
{
    // причина: RecommendationsClient — transient (из HttpClientFactory), нельзя инжектить
    // в singleton. Создаём scope в момент тренировки и резолвим оттуда — стандартный паттерн.
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecommendationsTrainCoordinator> _logger;

    // причина: одна Task на модель — повторные вызовы реюзают идущую тренировку
    private readonly ConcurrentDictionary<RecommendationModel, Task<bool>> _running = new();
    private readonly ConcurrentDictionary<RecommendationModel, DateTime> _lastTrainedAt = new();

    public RecommendationsTrainCoordinator(
        IServiceScopeFactory scopeFactory, ILogger<RecommendationsTrainCoordinator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Возвращает время последней удачной тренировки модели (или null).</summary>
    public DateTime? LastTrainedAt(RecommendationModel model)
        => _lastTrainedAt.TryGetValue(model, out var dt) ? dt : null;

    /// <summary>
    /// Триггерит асинхронное обучение модели. Если оно уже идёт — возвращает ту же Task.
    /// Не блокирует вызывающий код — он сам решает, ждать или сразу отдать fallback.
    /// </summary>
    public Task<bool> EnsureTrainingAsync(RecommendationModel model, CancellationToken ct = default)
    {
        return _running.GetOrAdd(model, m => StartTraining(m));
    }

    private async Task<bool> StartTraining(RecommendationModel model)
    {
        _logger.LogInformation("Запуск тренировки модели {Model}...", model);
        try
        {
            // причина: scope живёт только на время вызова — HttpClient берётся свежий из фабрики
            using var scope = _scopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<RecommendationsClient>();

            var ok = await client.TrainAsync(model, CancellationToken.None);
            if (ok)
            {
                _lastTrainedAt[model] = DateTime.UtcNow;
                _logger.LogInformation("Тренировка {Model} завершена", model);
            }
            else
            {
                _logger.LogWarning("Тренировка {Model} вернула не-2xx", model);
            }
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при тренировке {Model}", model);
            return false;
        }
        finally
        {
            // причина: убираем из running, чтобы следующий вызов мог снова инициировать train
            _running.TryRemove(model, out _);
        }
    }
}
