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

        if (overdue.Count == 0) return 0;

        foreach (var b in overdue)
        {
            b.Expire(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Đã tự động hết hạn {Count} đơn đặt chỗ quá hạn 15 phút.", overdue.Count);
        return overdue.Count;
    }
}
