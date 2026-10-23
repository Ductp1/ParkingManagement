using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SupportService.Application.Interfaces;
using SupportService.Infrastructure.Persistence;

namespace SupportService.Infrastructure.Workers;

/// <summary>
/// US-089 AC2: định kỳ chuyển các tranh chấp quá 48 giờ chủ bãi chưa phản hồi sang Escalated.
/// Cấu hình: "Complaints:Escalation:Enabled" (mặc định true), "Complaints:Escalation:IntervalSeconds" (mặc định 300).
/// </summary>
/// <remarks>
/// Chạy được trên nhiều instance của SupportService: mỗi lượt giữ 1 PostgreSQL advisory lock nên chỉ 1 instance escalate
/// tại một thời điểm. Lớp bảo vệ thứ hai là RowVersion của Complaint (lưu trùng → bị từ chối, không phát sự kiện trùng).
/// </remarks>
public sealed class ComplaintEscalationWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<ComplaintEscalationWorker> logger)
    : BackgroundService
{
    /// <summary>Khóa advisory riêng cho job escalate tranh chấp của SupportService.</summary>
    public const long AdvisoryLockKey = 908_903_089;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Complaints:Escalation:Enabled", true)) return;
        var interval = TimeSpan.FromSeconds(Math.Max(10, configuration.GetValue("Complaints:Escalation:IntervalSeconds", 300)));

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Worker escalate tranh chấp không chạy được lượt hiện tại."); }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>1 lượt escalate. Trả false nếu instance khác đang giữ khóa (lượt này bỏ qua).</summary>
    public async Task<bool> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        // Service Scoped (dùng DbContext) → mỗi lượt chạy tạo scope riêng; escalation service dùng chung DbContext/kết nối này.
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();

        // Advisory lock gắn với kết nối → mở kết nối thủ công để khóa, xử lý và mở khóa trên cùng 1 kết nối.
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var acquired = await db.Database
                .SqlQuery<bool>($"SELECT pg_try_advisory_lock({AdvisoryLockKey}) AS \"Value\"")
                .SingleAsync(cancellationToken);
            if (!acquired)
            {
                logger.LogDebug("Bỏ qua lượt escalate: instance khác đang chạy.");
                return false;
            }

            try
            {
                var count = await scope.ServiceProvider.GetRequiredService<IComplaintEscalationService>().EscalateOverdueAsync(cancellationToken);
                if (count > 0) logger.LogInformation("Đã chuyển {Count} tranh chấp quá hạn 48 giờ cho nền tảng xử lý.", count);
                return true;
            }
            finally
            {
                await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({AdvisoryLockKey})", CancellationToken.None);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
