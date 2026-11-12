using BookingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingManagement.SharedKernel.Enums;

namespace BookingService.Infrastructure.Workers;

/// <summary>
/// US-040: Tham số worker chốt phiên đỗ – bind từ appsettings "SessionCompletion", mặc định theo UC-49.
/// </summary>
public sealed class SessionCompletionOptions
{
    public int IntervalSeconds { get; init; } = 60;
    /// <summary>UC-49 bước 2: không có check-in, quá giờ bắt đầu + 2h → nhắc Tài xế và Chủ bãi.</summary>
    public int RemindAfterStartMinutes { get; init; } = 120;
    /// <summary>UC-49 bước 1: có check-in, không check-out, quá giờ kết thúc + 12h → Auto-Completed.</summary>
    public int AutoCompleteAfterEndHours { get; init; } = 12;
    /// <summary>UC-49 bước 3: vẫn không có dữ liệu, quá giờ kết thúc + 6h → No-show (không hoàn tiền).</summary>
    public int NoShowAfterEndHours { get; init; } = 6;
    /// <summary>Số booking tối đa xử lý cho mỗi bước trong 1 lượt quét – tránh nạp quá nhiều bản ghi khi tồn đọng.</summary>
    public int BatchSize { get; init; } = 200;

    public static SessionCompletionOptions Default { get; } = new();
}

/// <summary>
/// US-040 / UC-49: Worker nền của BookingService – phát hiện booking thiếu dữ liệu check-in/check-out
/// và tự động xử lý các booking đủ điều kiện (Auto-Completed hoặc No-show), có nhắc Tài xế/Chủ bãi trước.
/// Chống xử lý trùng bằng cột Bookings.AutoProcessStage (None → Reminded → Finalized) + điều kiện trạng thái,
/// chạy lại nhiều lần an toàn (idempotent). Chạy định kỳ mỗi IntervalSeconds giây (mặc định 60s).
/// </summary>
public sealed class SessionCompletionWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SessionCompletionWorker> logger,
    TimeProvider? timeProvider = null,
    SessionCompletionOptions? options = null) : BackgroundService
{
    private readonly SessionCompletionOptions _options = options ?? SessionCompletionOptions.Default;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SessionCompletionWorker (US-040) đã khởi động.");
        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.IntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessStaleBookingsAsync(stoppingToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Instance khác (hoặc thao tác cổng) vừa cập nhật cùng booking – lượt này bỏ qua, lượt sau quét lại theo trạng thái mới.
                logger.LogInformation("SessionCompletionWorker: booking vừa được cập nhật bởi tiến trình khác, sẽ xử lý lại ở lượt quét sau.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Lỗi khi quét booking thiếu dữ liệu check-in/out trong SessionCompletionWorker.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    /// <summary>Một lượt quét: tự hoàn tất → nhắc thiếu check-in → tự chốt No-show. Trả về số booking đã xử lý.</summary>
    public async Task<int> ProcessStaleBookingsAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var processed = 0;
        var batchSize = Math.Max(1, _options.BatchSize);

        // ===== UC-49 bước 1: có check-in, thiếu check-out, quá giờ kết thúc + 12h → Auto-Completed =====
        var autoComplete = await db.Bookings
            .Where(b => (b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Parking)
                        && b.AutoProcessStage != BookingAutoStage.Finalized
                        && b.EndAtUtc.AddHours(_options.AutoCompleteAfterEndHours) < now)
            .OrderBy(b => b.EndAtUtc).Take(batchSize)
            .ToListAsync(cancellationToken);
        foreach (var booking in autoComplete)
        {
            booking.CompleteWithoutCheckout(now, _options.AutoCompleteAfterEndHours);
            booking.AutoProcessStage = BookingAutoStage.Finalized;
            processed++;
        }
        if (autoComplete.Count > 0)
            logger.LogInformation("US-040: Tự động hoàn tất {Count} booking có check-in nhưng thiếu check-out.", autoComplete.Count);

        // ===== UC-49 bước 2: không có check-in, quá giờ bắt đầu + 2h → nhắc Tài xế và Chủ bãi =====
        var toRemind = await db.Bookings
            .Where(b => b.Status == BookingStatus.Confirmed
                        && b.CheckedInAtUtc == null
                        && b.AutoProcessStage == BookingAutoStage.None
                        && b.StartAtUtc.AddMinutes(_options.RemindAfterStartMinutes) < now)
            .OrderBy(b => b.StartAtUtc).Take(batchSize)
            .ToListAsync(cancellationToken);
        foreach (var booking in toRemind)
        {
            booking.RemindMissingCheckIn(now, _options.RemindAfterStartMinutes);
            processed++;
        }
        if (toRemind.Count > 0)
            logger.LogInformation("US-040: Đã đánh dấu nhắc {Count} booking chưa check-in sau {Minutes} phút.",
                toRemind.Count, _options.RemindAfterStartMinutes);

        // ===== UC-49 bước 3: vẫn không có dữ liệu, quá giờ kết thúc + 6h → No-show (không hoàn tiền) =====
        var toNoShow = await db.Bookings
            .Where(b => b.Status == BookingStatus.Confirmed
                        && b.CheckedInAtUtc == null
                        && b.AutoProcessStage != BookingAutoStage.Finalized
                        && b.EndAtUtc.AddHours(_options.NoShowAfterEndHours) < now)
            .OrderBy(b => b.EndAtUtc).Take(batchSize)
            .ToListAsync(cancellationToken);
        foreach (var booking in toNoShow)
        {
            booking.FinalizeNoShow(now, _options.NoShowAfterEndHours);
            booking.AutoProcessStage = BookingAutoStage.Finalized;
            processed++;
        }
        if (toNoShow.Count > 0)
            logger.LogInformation("US-040: Tự động chốt No-show {Count} booking thiếu toàn bộ dữ liệu check-in/out.", toNoShow.Count);

        if (processed > 0)
            await db.SaveChangesAsync(cancellationToken);

        return processed;
    }
}