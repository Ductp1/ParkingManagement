using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingService.Application.Features.LotOperations;
using ParkingService.Domain.Entities;
using ParkingManagement.SharedKernel.Contracts;
namespace ParkingService.Infrastructure.Workers;

/// <summary>
/// US-068: worker đánh giá stale cho mọi bãi Active mỗi 30s (threshold config LotHeartbeat:StaleAfterMinutes,
/// mặc định 3 phút). Chuyển trạng thái → phát LotDataMarkedStale / LotDataRecovered ra outbox.
/// Khôi phục tự động khi heartbeat resume (RecordHeartbeat ở endpoint hoặc worker nhận lại heartbeat mới).
/// </summary>
public sealed class StalenessMonitorWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<StalenessOptions> options,
    TimeProvider timeProvider,
    ILogger<StalenessMonitorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("StalenessMonitorWorker khởi động (threshold {Minutes} phút).", options.Value.StaleAfterMinutes);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateAllAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Lỗi vòng đánh giá stale.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(options.Value.PollSeconds, 10, 600)), stoppingToken);
        }
    }

    public async Task<int> EvaluateAllAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var threshold = TimeSpan.FromMinutes(Math.Clamp(options.Value.StaleAfterMinutes, 1, 120));
        var transitions = 0;

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILotOperationsRepository>();
        var events = scope.ServiceProvider.GetRequiredService<ILotEventPublisher>();
        var lots = await repository.GetActiveLotsAsync(cancellationToken);

        foreach (var lot in lots)
        {
            if (StalenessEvaluator.NeedsTransitionToStale(lot.LastHeartbeatAtUtc, lot.IsDataStale, now, threshold))
            {
                lot.MarkStaleIfOverdue(now, threshold);
                events.Enqueue(new LotDataMarkedStale(lot.Id, lot.Name, lot.OwnerProfileId, lot.LastHeartbeatAtUtc, now));
                transitions++;
            }
            else if (lot.IsDataStale && lot.LastHeartbeatAtUtc is not null && now - lot.LastHeartbeatAtUtc.Value <= threshold)
            {
                // Heartbeat đã resume (RecordHeartbeat đặt IsDataStale=false nên nhánh này hiếm; giữ để chắc chắn).
                lot.RecordHeartbeat(lot.LastHeartbeatAtUtc.Value);
                events.Enqueue(new LotDataRecovered(lot.Id, lot.Name, lot.OwnerProfileId, lot.LastHeartbeatAtUtc.Value));
                transitions++;
            }
        }

        if (transitions > 0)
        {
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogInformation("US-068: {Count} bãi chuyển trạng thái stale/fresh.", transitions);
        }
        return transitions;
    }
}

