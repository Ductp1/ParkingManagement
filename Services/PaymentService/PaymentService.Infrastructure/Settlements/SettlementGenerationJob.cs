using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Settlements;

namespace PaymentService.Infrastructure.Settlements;

/// <summary>Lịch chạy job sinh kỳ ("Settlements:Generation:Enabled", "Settlements:Generation:IntervalMinutes").</summary>
public sealed record SettlementJobSchedule(bool Enabled, TimeSpan Interval)
{
    public const int DefaultIntervalMinutes = 60;
    public const int MinIntervalMinutes = 1;
}

/// <summary>
/// US-080 (UC-43 "Timer tạo kỳ quyết toán"): chạy <see cref="IRunSettlementGenerationUseCase"/> khi service khởi động rồi định kỳ.
/// Mỗi lượt một scope (DbContext riêng). Nguồn lỗi / lỗi bất ngờ chỉ được log – lượt sau làm lại (idempotent).
/// </summary>
public sealed class SettlementGenerationJob(IServiceScopeFactory scopeFactory, SettlementJobSchedule schedule, TimeProvider timeProvider,
    ILogger<SettlementGenerationJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!schedule.Enabled)
        {
            logger.LogInformation("US-080: job sinh kỳ quyết toán đang tắt (Settlements:Generation:Enabled = false).");
            return;
        }

        using var timer = new PeriodicTimer(schedule.Interval, timeProvider);
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    public async Task<SettlementRunResult?> RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IRunSettlementGenerationUseCase>().ExecuteAsync(cancellationToken);
            if (result.Skipped) logger.LogDebug("US-080: instance khác đang sinh kỳ quyết toán – bỏ lượt này.");
            else if (result.Period is null) logger.LogDebug("US-080: chưa có kỳ quyết toán nào kết thúc.");
            else
                logger.Log(result.Created > 0 ? LogLevel.Information : LogLevel.Debug,
                    "US-080: kỳ {PeriodStart}–{PeriodEnd}: sinh {Created} kỳ, bỏ qua {Skipped} chủ bãi.",
                    result.Period.PeriodStart, result.Period.PeriodEnd, result.Created, result.SkippedOwners);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (DependencyUnavailableException ex)
        {
            logger.LogWarning("US-080: nguồn dữ liệu chưa sẵn sàng, không sinh kỳ ở lượt này – {Detail}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "US-080: lượt sinh kỳ quyết toán lỗi – sẽ thử lại ở lượt sau.");
            return null;
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
