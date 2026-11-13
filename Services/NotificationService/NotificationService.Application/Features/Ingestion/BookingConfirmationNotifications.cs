using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Application.Features.Ingestion;

/// <summary>
/// US-083: xác nhận booking & thanh toán. consumer gọi sau khi TransactionalInbox chặn trùng EventId —
/// tiếp tục idempotent ở lớp kênh qua DedupeKey "evt:{EventId}:{Channel}" (webhook replay an toàn tuyệt đối).
/// Thông báo giao dịch là BẮT BUỘC (bỏ qua preference tắt kênh — entity NotificationPreference đã ghi chú).
/// </summary>
public interface IBookingConfirmationNotifications
{
    Task HandleBookingConfirmedAsync(BookingConfirmed evt, Guid eventId, CancellationToken cancellationToken = default);
    Task HandleBookingPaymentCompletedAsync(BookingPaymentCompleted evt, Guid eventId, CancellationToken cancellationToken = default);
}

public sealed class BookingConfirmationNotifications(
    ITemplateRenderer renderer,
    IUserNotificationSettingsPort settings,
    ITransactionalNotificationSender sender) : IBookingConfirmationNotifications
{
    public async Task HandleBookingConfirmedAsync(BookingConfirmed evt, Guid eventId, CancellationToken cancellationToken = default)
    {
        var language = await settings.GetLanguageAsync(evt.UserId, cancellationToken);
        var placeholders = new Dictionary<string, string>
        {
            ["BookingCode"] = evt.Code,
            ["LotName"] = evt.ParkingLotName,
            ["SlotCode"] = string.IsNullOrWhiteSpace(evt.SlotCode) ? "tự chọn" : evt.SlotCode!,
            ["StartAt"] = VietnamTime.Format(evt.StartAtUtc),
            ["EndAt"] = VietnamTime.Format(evt.EndAtUtc),
            ["Amount"] = FormatVnd(evt.TotalAmount),
        };
        var dataJson = System.Text.Json.JsonSerializer.Serialize(new { bookingId = evt.BookingId, lotId = evt.ParkingLotId });
        // InApp (real-time qua /hubs/notify) + Email — mỗi kênh 1 DedupeKey riêng.
        await SendAsync(evt.UserId, NotificationChannel.InApp, "BOOKING_CONFIRMED", language, placeholders, dataJson, eventId, cancellationToken);
        await SendAsync(evt.UserId, NotificationChannel.Email, "BOOKING_CONFIRMED", language, placeholders, dataJson, eventId, cancellationToken);
    }

    public async Task HandleBookingPaymentCompletedAsync(BookingPaymentCompleted evt, Guid eventId, CancellationToken cancellationToken = default)
    {
        var language = await settings.GetLanguageAsync(evt.UserId, cancellationToken);
        var dataJson = System.Text.Json.JsonSerializer.Serialize(new { bookingId = evt.BookingId, paymentId = evt.PaymentId });

        if (evt.Succeeded)
        {
            var placeholders = new Dictionary<string, string>
            {
                ["BookingCode"] = evt.Code,
                ["Amount"] = FormatVnd(evt.Amount),
                ["Method"] = evt.Method,
            };
            await SendAsync(evt.UserId, NotificationChannel.InApp, "PAYMENT_SUCCEEDED", language, placeholders, dataJson, eventId, cancellationToken);
        }
        else
        {
            var placeholders = new Dictionary<string, string>
            {
                ["BookingCode"] = evt.Code,
                ["Reason"] = evt.FailureReason ?? "không rõ lý do",
            };
            await SendAsync(evt.UserId, NotificationChannel.InApp, "PAYMENT_FAILED", language, placeholders, dataJson, eventId, cancellationToken);
        }
    }

    private async Task SendAsync(int userId, NotificationChannel channel, string templateKey, string language,
        IReadOnlyDictionary<string, string> placeholders, string dataJson, Guid eventId, CancellationToken cancellationToken)
    {
        var rendered = await renderer.RenderAsync(templateKey, channel, language, placeholders, cancellationToken);
        await sender.SendAsync(userId, channel, templateKey, rendered.Title, rendered.Body, dataJson,
            $"evt:{eventId}:{channel}", cancellationToken);
    }

    private static string FormatVnd(decimal amount) => amount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " ₫";
}
