using AdminService.Application.Features.Sanctions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdminService.Infrastructure.Jobs;

/// <summary>
/// "Sanctions:ExpiryJob": { "Enabled": true, "IntervalSeconds": 300, "BatchSize": 100 }. Interval 30–3600 giây, batch 1–500;
/// thiếu / ngoài khoảng → mặc định.
/// </summary>
public sealed record SanctionExpiryJobSettings(bool Enabled = true, int IntervalSeconds = 300, int BatchSize = 100);

/// <summary>
/// US-097: định kỳ đóng chế tài SLA tới hạn (IExpireSanctionsUseCase). Mỗi lượt một scope DI riêng (DbContext mới);
/// lỗi một lượt chỉ ghi log, lượt sau chạy tiếp. Chạy trùng nhiều instance an toàn: use case chỉ đóng dòng còn Active.
/// </summary>
public sealed class SanctionExpiryJob(IServiceScopeFactory scopeFactory, SanctionExpiryJobSettings settings,
    ILogger<SanctionExpiryJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.IntervalSeconds));
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await WaitNextAsync(timer, stoppingToken));
    }

    /// <summary>Một lượt: đóng theo lô tới khi hết chế tài tới hạn (lô cuối nhỏ hơn BatchSize).</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            int expired;
            do
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var useCase = scope.ServiceProvider.GetRequiredService<IExpireSanctionsUseCase>();
                expired = await useCase.ExecuteAsync(settings.BatchSize, cancellationToken);
            }
            while (expired == settings.BatchSize && !cancellationToken.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Đang tắt ứng dụng.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SanctionExpiryJob lỗi, sẽ thử lại sau {Seconds} giây.", settings.IntervalSeconds);
        }
    }

    private static async Task<bool> WaitNextAsync(PeriodicTimer timer, CancellationToken stoppingToken)
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
