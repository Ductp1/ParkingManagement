using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Domain;

namespace ParkingManagement.ServiceDefaults.Messaging;

public interface IOutboxTransport { Task DeliverAsync(OutboxMessage message, CancellationToken ct); }

// Có thể tái sử dụng cho cả 9 DbContext. Delivery là at-least-once, receiver phải dùng EventId để chống lặp.
public sealed class OutboxDispatcher<TContext>(IServiceScopeFactory scopes, IConfiguration config,
    ILogger<OutboxDispatcher<TContext>> logger, TimeProvider clock) : BackgroundService where TContext : ServiceDbContext
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue<bool>("EventBus:Enabled")) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchOneAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Outbox worker không xử lý được lượt hiện tại."); }
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
    public async Task DispatchOneAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var maxAttempts = Math.Clamp(config.GetValue("EventBus:MaxAttempts", 10), 1, 100);
        var messages = await db.OutboxMessages.FromSqlInterpolated($"SELECT * FROM \"OutboxMessages\" WHERE \"ProcessedAtUtc\" IS NULL AND \"AttemptCount\" < {maxAttempts} AND (\"NextAttemptAtUtc\" IS NULL OR \"NextAttemptAtUtc\" <= {now}) ORDER BY \"OccurredAtUtc\" LIMIT 1 FOR UPDATE SKIP LOCKED").ToListAsync(ct);
        if (messages.Count == 0) { await tx.CommitAsync(ct); return; }
        var message = messages[0];
        try
        {
            await scope.ServiceProvider.GetRequiredService<IOutboxTransport>().DeliverAsync(message, ct);
            message.ProcessedAtUtc = clock.GetUtcNow().UtcDateTime; message.LastError = null; message.NextAttemptAtUtc = null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            message.AttemptCount++;
            // Chỉ lưu loại lỗi: không lưu URL/response body có OTP hoặc dữ liệu riêng tư.
            message.LastError = ex.GetType().Name;
            message.NextAttemptAtUtc = now.AddSeconds(Math.Min(3600, Math.Pow(2, message.AttemptCount) * 5));
            logger.LogWarning("Outbox {EventId} thất bại lần {AttemptCount}.", message.Id, message.AttemptCount);
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
