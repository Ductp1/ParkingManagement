using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Application.Features.LotOperations;

namespace ParkingService.Infrastructure.Workers;

/// <summary>
/// US-068: worker nền định kỳ quét các bãi Active quá hạn heartbeat → chuyển stale +
/// phát LotDataMarkedStale (NotificationService cảnh báo chủ bãi, dashboard hiển thị "dữ liệu cũ").
/// Mỗi lượt quét tạo 1 DI scope riêng — repo + publisher trong scope dùng chung ParkingDbContext
/// nên mark-stale + outbox flush atomic. Heartbeat resume (LotDataRecovered) do
/// RecordHeartbeatUseCase phát khi thiết bị quay lại, worker không lo phần đó.
/// </summary>
public sealed class StalenessMonitorWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<StalenessOptions> options,
    TimeProvider timeProvider,
    ILogger<StalenessMonitorWorker> logger) : BackgroundService
{
    private readonly StalenessOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "🛰 StalenessMonitorWorker khởi động: quét mỗi {PollSeconds}s, stale sau {StaleAfterMinutes} phút không heartbeat.",
            _options.PollSeconds, _options.StaleAfterMinutes);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _options.PollSeconds)));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await EvaluateAllAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Lỗi tạm thời (DB timeout…) — vòng sau quét lại, transition idempotent nên không lo lặp.
                    logger.LogError(ex, "StalenessMonitorWorker lượt quét hiện tại thất bại, thử lại ở vòng sau.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { /* shutdown */ }
    }

    /// <summary>Quét 1 lượt toàn bộ bãi Active. Trả về số bãi vừa chuyển sang stale.</summary>
    public async Task<int> EvaluateAllAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILotOperationsRepository>();
        var events = scope.ServiceProvider.GetRequiredService<ILotEventPublisher>();

        var lots = await repository.GetActiveLotsAsync(cancellationToken);
        var threshold = TimeSpan.FromMinutes(_options.StaleAfterMinutes);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var transitions = 0;

        foreach (var lot in lots)
        {
            if (!lot.MarkStaleIfOverdue(now, threshold))
                continue;

            transitions++;
            events.Enqueue(new LotDataMarkedStale(lot.Id, lot.Name, lot.OwnerProfileId, lot.LastHeartbeatAtUtc, now));
            logger.LogWarning("🟠 Lot {LotId} ({LotName}) chuyển STALE: heartbeat cuối {LastHeartbeatAtUtc:O}.",
                lot.Id, lot.Name, lot.LastHeartbeatAtUtc);
        }

        if (transitions > 0)
            await repository.SaveChangesAsync(cancellationToken);

        return transitions;
    }
}