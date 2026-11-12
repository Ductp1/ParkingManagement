using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using BookingService.Infrastructure.Persistence;
using ParkingManagement.SharedKernel.Enums;

namespace BookingService.Infrastructure.Workers;

/// <summary>
/// US-023: Tự động hết hạn giữ chỗ (Expired) sau 15 phút không thanh toán.
/// Chạy định kỳ mỗi 60 giây để quét các booking PendingPayment quá hạn.
/// </summary>
public sealed class HoldExpiryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<HoldExpiryWorker> logger,
    TimeProvider? timeProvider = null) : BackgroundService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("HoldExpiryWorker đã khởi động.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireOverdueBookingsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Lỗi khi quét hết hạn booking trong HoldExpiryWorker.");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }

    public async Task<int> ExpireOverdueBookingsAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var overdue = await db.Bookings
            .Where(b => b.Status == BookingStatus.PendingPayment && b.HoldExpiresAtUtc != null && b.HoldExpiresAtUtc < now)
            .ToListAsync(cancellationToken);

        // Quét thêm các đơn PendingOwnerApproval quá 15 phút (CreatedAtUtc < now - 15p) mà chủ bãi không duyệt
        var approvalCutoff = now.AddMinutes(-15);
        var overdueApprovals = await db.Bookings
            .Where(b => b.Status == BookingStatus.PendingOwnerApproval && b.CreatedAtUtc < approvalCutoff)
            .ToListAsync(cancellationToken);

        if (overdue.Count == 0 && overdueApprovals.Count == 0) return 0;

        foreach (var b in overdue)
        {
            b.Expire(now);
        }

        foreach (var b in overdueApprovals)
        {
            b.CancelByOwner(now, 0, "Quá 15 phút chủ bãi không phản hồi, tự động hủy để giải phóng chỗ");
        }

        await db.SaveChangesAsync(cancellationToken);
        var total = overdue.Count + overdueApprovals.Count;
        logger.LogInformation("Đã tự động xử lý {Count} đơn đặt chỗ quá hạn (PendingPayment: {PendingCount}, PendingOwnerApproval: {ApprovalCount}).",
            total, overdue.Count, overdueApprovals.Count);
        return total;
    }
}
