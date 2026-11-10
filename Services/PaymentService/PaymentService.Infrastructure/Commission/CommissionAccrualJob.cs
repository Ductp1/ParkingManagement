using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Features.Commission;

namespace PaymentService.Infrastructure.Commission;

/// <summary>Lịch chạy job hoa hồng ("Commission:Enabled", "Commission:IntervalSeconds").</summary>
public sealed record CommissionJobSchedule(bool Enabled, TimeSpan Interval)
{
    public const int DefaultIntervalSeconds = 60;
    public const int MinIntervalSeconds = 5;
}

/// <summary>
/// US-079: chạy <see cref="IRunCommissionAccrualUseCase"/> ngay khi service khởi động (sau migration) rồi định kỳ.
/// Mỗi lượt một scope (DbContext riêng). Tắt khi Commission:Enabled = false; thiếu cấu hình AdminService vẫn chạy (Reversal không cần
/// AdminService), giao dịch mới để chờ tính và log cảnh báo. Lỗi của một lượt chỉ được log – lượt sau làm lại.
/// </summary>
public sealed class CommissionAccrualJob(IServiceScopeFactory scopeFactory, CommissionJobSchedule schedule, AdminServiceConnection adminService,
    TimeProvider timeProvider, ILogger<CommissionAccrualJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!schedule.Enabled)
        {
            logger.LogInformation("US-079: job tính hoa hồng đang tắt (Commission:Enabled = false).");
            return;
        }
        if (!adminService.IsConfigured)
            logger.LogWarning("US-079: chưa cấu hình {BaseUrl} / {ApiKey} – giao dịch mới sẽ ở trạng thái chờ tính hoa hồng.",
                AdminServiceConnection.BaseUrlKey, AdminServiceConnection.ApiKeyKey);

        using var timer = new PeriodicTimer(schedule.Interval, timeProvider);
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    public async Task<CommissionRunResult?> RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IRunCommissionAccrualUseCase>().ExecuteAsync(cancellationToken);
            if (result.Skipped) logger.LogDebug("US-079: instance khác đang tính hoa hồng – bỏ lượt này.");
            else
                // Lượt không có việc → Debug để log không bị lặp mỗi phút; có xử lý → Information (use case log chi tiết).
                logger.Log(result.Processed > 0 ? LogLevel.Information : LogLevel.Debug,
                    "US-079: lượt tính hoa hồng xử lý {Processed} giao dịch (Accrual {Accrued}, Reversal {Reversed}).",
                    result.Processed, result.Accrued, result.Reversed);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "US-079: lượt tính hoa hồng lỗi – sẽ thử lại ở lượt sau.");
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
