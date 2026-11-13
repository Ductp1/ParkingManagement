using NotificationService.Application.Features.Dispatcher;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Application.Features.Ingestion;

/// <summary>
/// US-085: thông báo cho CHỦ BÃI khi có booking mới/hủy/đổi tại bãi của họ.
/// Scope: event payload luôn gắn OwnerProfileId → chỉ chủ bãi đó nhận (không broadcast).
/// Kênh: InApp (real-time qua /hubs/notify) — phát triển thêm Email/SMS chỉ cần thêm SendAsync.
/// </summary>
public interface IOwnerBookingNotifications
{
    Task HandleBookingCreatedAsync(BookingCreated evt, Guid eventId, CancellationToken cancellationToken = default);
    Task HandleBookingCancelledAsync(BookingCancelled evt, Guid eventId, CancellationToken cancellationToken = default);
    Task HandleBookingModifiedAsync(BookingModified evt, Guid eventId, CancellationToken cancellationToken = default);
}

public sealed class OwnerBookingNotifications(
    IUserDirectory userDirectory,
    IUserNotificationSettingsPort settings,
    ITemplateRenderer renderer,
    ITransactionalNotificationSender sender) : IOwnerBookingNotifications
{
    public async Task HandleBookingCreatedAsync(BookingCreated evt, Guid eventId, CancellationToken cancellationToken = default)
        => await NotifyOwnerAsync(evt.OwnerProfileId, evt.UserId, "OWNER_BOOKING_NEW", new Dictionary<string, string>
        {
            ["BookingCode"] = evt.Code,
            ["LotName"] = evt.ParkingLotName,
            ["StartAt"] = VietnamTime.Format(evt.StartAtUtc),
            ["EndAt"] = VietnamTime.Format(evt.EndAtUtc),
        }, new { bookingId = evt.BookingId, lotId = evt.ParkingLotId }, eventId, cancellationToken);

    public async Task HandleBookingCancelledAsync(BookingCancelled evt, Guid eventId, CancellationToken cancellationToken = default)
        => await NotifyOwnerAsync(evt.OwnerProfileId, evt.UserId, "OWNER_BOOKING_CANCELLED", new Dictionary<string, string>
        {
            ["BookingCode"] = evt.Code,
            ["LotName"] = evt.ParkingLotName,
            ["Reason"] = evt.Reason,
        }, new { bookingId = evt.BookingId, lotId = evt.ParkingLotId }, eventId, cancellationToken);

    public async Task HandleBookingModifiedAsync(BookingModified evt, Guid eventId, CancellationToken cancellationToken = default)
        => await NotifyOwnerAsync(evt.OwnerProfileId, evt.UserId, "OWNER_BOOKING_MODIFIED", new Dictionary<string, string>
        {
            ["BookingCode"] = evt.Code,
            ["LotName"] = evt.ParkingLotName,
            ["ChangedNote"] = evt.ChangedNote,
            ["StartAt"] = VietnamTime.Format(evt.StartAtUtc),
            ["EndAt"] = VietnamTime.Format(evt.EndAtUtc),
        }, new { bookingId = evt.BookingId, lotId = evt.ParkingLotId }, eventId, cancellationToken);

    private async Task NotifyOwnerAsync(int ownerProfileId, int driverUserId, string templateKey,
        Dictionary<string, string> placeholders, object dataPayload, Guid eventId, CancellationToken cancellationToken)
    {
        var ownerUserId = await userDirectory.GetOwnerUserIdAsync(ownerProfileId, cancellationToken);
        if (ownerUserId is null or <= 0)
            return; // hồ sơ chủ bãi chưa map được user — bỏ qua (log ở handler qua exception? giữ im lặng an toàn)

        var language = await settings.GetLanguageAsync(ownerUserId.Value, cancellationToken);
        var rendered = await renderer.RenderAsync(templateKey, NotificationChannel.InApp, language, placeholders, cancellationToken);
        await sender.SendAsync(ownerUserId.Value, NotificationChannel.InApp, templateKey, rendered.Title, rendered.Body,
            System.Text.Json.JsonSerializer.Serialize(dataPayload), $"evt:{eventId}:Owner", cancellationToken);
        _ = driverUserId; // (reserved) nhân viên trực cùng bãi nhận thêm khi có bảng phân ca staff↔lot
    }
}
