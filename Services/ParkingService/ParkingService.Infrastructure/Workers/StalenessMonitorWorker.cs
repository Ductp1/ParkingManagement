using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.LotHeartbeat;
using ParkingService.Domain;

namespace ParkingService.Infrastructure.Workers;

/// <summary>
/// US-068: BackgroundService quét mọi bãi Active mỗi <see cref="LotHeartbeatSettings.PollSeconds"/> giây;
/// quá <see cref="LotHeartbeatSettings.StaleAfterMinutes"/> không heartbeat → MarkStale() + phát
/// LotDataMarkedStale qua outbox (idempotent: chỉ phát đúng một lần cho mỗi lần chuyển Fresh → Stale).
/// Heartbeat mới nhận được sẽ tự phục hồi stale và phát LotDataRecovered tại use case ghi nhận heartbeat.
/// </summary>
public sealed class StalenessMonitorWorker(
    IServiceScopeFactory scopeFactory,
    LotHeartbeatSettings settings,
    TimeProvider clock,
    ILogger<StalenessMonitorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "StalenessMonitorWorker khởi động: PollSeconds={PollSeconds}, StaleAfterMinutes={StaleAfterMinutes}",
            settings.PollSeconds, settings.StaleAfterMinutes);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, settings.PollSeconds)));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Một lượt quét lỗi không được phép làm worker chết – lượt sau sẽ quét lại.
                logger.LogError(ex, "StalenessMonitorWorker: lượt quét stale thất bại, sẽ thử lại ở chu kỳ kế tiếp.");
            }
        }

        logger.LogInformation("StalenessMonitorWorker đã dừng.");
    }

    /// <summary>Điểm vào cho unit test – chạy một lượt quét ngay (không qua timer).</summary>
    public Task SweepForTest(CancellationToken cancellationToken = default) => SweepAsync(cancellationToken);

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var stalenessRepository = scope.ServiceProvider.GetRequiredService<ILotStalenessRepository>();

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var events = new List<IntegrationEvent>();

        foreach (var lot in await stalenessRepository.ListActiveAsync(cancellationToken))
        {
            if (StalenessEvaluator.IsStale(lot.LastHeartbeatAtUtc, settings.StaleAfter, nowUtc) && lot.MarkStale())
                events.Add(new LotDataMarkedStale(lot.Id, lot.OwnerProfileId, lot.LastHeartbeatAtUtc));
        }

        if (events.Count > 0)
        {
            await stalenessRepository.SaveWithEventsAsync(events, cancellationToken);
            logger.LogWarning(
                "US-068 đánh dấu {Count} bãi stale (StaleAfterMinutes={StaleAfterMinutes}): {LotIds}",
                events.Count, settings.StaleAfterMinutes,
                string.Join(',', events.Cast<LotDataMarkedStale>().Select(e => e.ParkingLotId)));
        }
    }
}
