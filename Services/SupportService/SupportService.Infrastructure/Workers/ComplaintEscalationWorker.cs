using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SupportService.Application.Interfaces;

namespace SupportService.Infrastructure.Workers;

/// <summary>
/// US-089 AC2: định kỳ chuyển các tranh chấp quá 48 giờ chủ bãi chưa phản hồi sang Escalated.
/// Cấu hình: "Complaints:Escalation:Enabled" (mặc định true), "Complaints:Escalation:IntervalSeconds" (mặc định 300).
/// </summary>
public sealed class ComplaintEscalationWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<ComplaintEscalationWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Complaints:Escalation:Enabled", true)) return;
        var interval = TimeSpan.FromSeconds(Math.Max(10, configuration.GetValue("Complaints:Escalation:IntervalSeconds", 300)));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Service Scoped (dùng DbContext) → mỗi lượt chạy tạo scope riêng.
                await using var scope = scopes.CreateAsyncScope();
                var count = await scope.ServiceProvider.GetRequiredService<IComplaintEscalationService>().EscalateOverdueAsync(stoppingToken);
                if (count > 0) logger.LogInformation("Đã chuyển {Count} tranh chấp quá hạn 48 giờ cho nền tảng xử lý.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Worker escalate tranh chấp không chạy được lượt hiện tại."); }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
