using NotificationService.Application.Features.Dispatcher;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Application.Features.Ingestion;

/// <summary>
/// US-086: thông báo khẩn cấp ĐA KÊNH (InApp + Push + Email) &amp; tracking trạng thái gửi.
/// - LotEmergencyStopped (từ US-069): fan-out mẫu EMERGENCY_CLOSURE cho TỪNG khách có booking bị ảnh hưởng.
/// - LotEmergencyLifted: LOT_EMERGENCY_LIFTED cho chủ bãi (bãi mở lại, nhận booking bình thường).
/// - LotDataMarkedStale / LotDataRecovered (từ US-068): LOT_DATA_STALE / LOT_DATA_RECOVERED cho chủ bãi.
/// Mỗi (event, user, channel) 1 DedupeKey riêng "evt:{eventId}:{user}:{channel}" → replay không tạo bản sao.
/// Mọi send lưu bảng Notifications (status Sent/Pending/Failed) → GET /api/v1/notifications/delivery-status?eventId= tra cứu.
/// Thông báo khẩn cấp là BẮT BUỘC — gửi trực tiếp qua sender, không qua NotificationPreference (giống US-083).
/// </summary>
public interface ILotEmergencyAlertNotifications
{
    Task HandleLotEmergencyStoppedAsync(LotEmergencyStopped evt, Guid eventId, CancellationToken cancellationToken = default);
    Task HandleLotEmergencyLiftedAsync(LotEmergencyLifted evt, Guid eventId, CancellationToken cancellationToken = default);
    Task HandleLotDataMarkedStaleAsync(LotDataMarkedStale evt, Guid eventId, CancellationToken cancellationToken = default);
    Task HandleLotDataRecoveredAsync(LotDataRecovered evt, Guid eventId, CancellationToken cancellationToken = default);
}

public sealed class LotEmergencyAlertNotifications(
    ITemplateRenderer renderer,
    IUserNotificationSettingsPort settings,
    ITransactionalNotificationSender sender,
    IUserDirectory userDirectory) : ILotEmergencyAlertNotifications
{
    /// <summary>Bộ kênh gửi cảnh báo khẩn cấp — InApp real-time qua /hubs/notify, Push (FCM) khi offline app, Email lưu vết.</summary>
    private static readonly NotificationChannel[] EmergencyChannels =
        [NotificationChannel.InApp, NotificationChannel.Push, NotificationChannel.Email];

    public async Task HandleLotEmergencyStoppedAsync(LotEmergencyStopped evt, Guid eventId, CancellationToken cancellationToken = default)
    {
        // Fan-out đa kênh cho từng khách có booking bị ảnh hưởng (1 user có thể có nhiều booking trong khung đóng).
        foreach (var booking in evt.AffectedBookings)
        {
            var language = await settings.GetLanguageAsync(booking.UserId, cancellationToken);
            var placeholders = new Dictionary<string, string>
            {
                ["LotName"] = evt.LotName,
                ["Reason"] = evt.Reason,
                ["BookingCode"] = booking.Code,
            };
            var dataJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                lotId = evt.ParkingLotId,
                bookingId = booking.BookingId,
                bookingCode = booking.Code,
                kind = "emergency-stop",
            });

            foreach (var channel in EmergencyChannels)
            {
                var rendered = await renderer.RenderAsync("EMERGENCY_CLOSURE", channel, language, placeholders, cancellationToken);
                await sender.SendAsync(booking.UserId, channel, "EMERGENCY_CLOSURE", rendered.Title, rendered.Body,
                    dataJson, $"evt:{eventId}:{booking.UserId}:{channel}", cancellationToken);
            }
        }
    }

    public async Task HandleLotEmergencyLiftedAsync(LotEmergencyLifted evt, Guid eventId, CancellationToken cancellationToken = default)
    {
        var ownerUserId = await userDirectory.GetOwnerUserIdAsync(evt.OwnerProfileId, cancellationToken);
        if (ownerUserId is null or <= 0)
            return; // hồ sơ chủ bãi chưa map được user — bỏ qua an toàn

        var language = await settings.GetLanguageAsync(ownerUserId.Value, cancellationToken);
        var placeholders = new Dictionary<string, string>
        {
            ["LotName"] = evt.LotName,
            ["Reason"] = evt.Reason,
        };
        var rendered = await renderer.RenderAsync("LOT_EMERGENCY_LIFTED", NotificationChannel.InApp, language, placeholders, cancellationToken);
        await sender.SendAsync(ownerUserId.Value, NotificationChannel.InApp, "LOT_EMERGENCY_LIFTED", rendered.Title, rendered.Body,
            System.Text.Json.JsonSerializer.Serialize(new { lotId = evt.ParkingLotId, kind = "emergency-lift" }),
            $"evt:{eventId}:Owner", cancellationToken);
    }

    public async Task HandleLotDataMarkedStaleAsync(LotDataMarkedStale evt, Guid eventId, CancellationToken cancellationToken = default)
    {
        var ownerUserId = await userDirectory.GetOwnerUserIdAsync(evt.OwnerProfileId, cancellationToken);
        if (ownerUserId is null or <= 0)
            return;

        var language = await settings.GetLanguageAsync(ownerUserId.Value, cancellationToken);
        var placeholders = new Dictionary<string, string>
        {
            ["LotName"] = evt.LotName,
            ["LastHeartbeat"] = evt.LastHeartbeatAtUtc is null ? "chưa có tín hiệu" : VietnamTime.Format(evt.LastHeartbeatAtUtc.Value),
        };
        var rendered = await renderer.RenderAsync("LOT_DATA_STALE", NotificationChannel.InApp, language, placeholders, cancellationToken);
        await sender.SendAsync(ownerUserId.Value, NotificationChannel.InApp, "LOT_DATA_STALE", rendered.Title, rendered.Body,
            System.Text.Json.JsonSerializer.Serialize(new { lotId = evt.ParkingLotId, kind = "stale" }),
            $"evt:{eventId}:Owner", cancellationToken);
    }

    public async Task HandleLotDataRecoveredAsync(LotDataRecovered evt, Guid eventId, CancellationToken cancellationToken = default)
    {
        var ownerUserId = await userDirectory.GetOwnerUserIdAsync(evt.OwnerProfileId, cancellationToken);
        if (ownerUserId is null or <= 0)
            return;

        var language = await settings.GetLanguageAsync(ownerUserId.Value, cancellationToken);
        var placeholders = new Dictionary<string, string>
        {
            ["LotName"] = evt.LotName,
            ["Heartbeat"] = VietnamTime.Format(evt.HeartbeatAtUtc),
        };
        var rendered = await renderer.RenderAsync("LOT_DATA_RECOVERED", NotificationChannel.InApp, language, placeholders, cancellationToken);
        await sender.SendAsync(ownerUserId.Value, NotificationChannel.InApp, "LOT_DATA_RECOVERED", rendered.Title, rendered.Body,
            System.Text.Json.JsonSerializer.Serialize(new { lotId = evt.ParkingLotId, kind = "recovered" }),
            $"evt:{eventId}:Owner", cancellationToken);
    }
}