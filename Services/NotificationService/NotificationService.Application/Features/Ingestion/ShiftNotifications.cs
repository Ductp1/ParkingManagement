using NotificationService.Application.Features.Dispatcher;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Application.Features.Ingestion;

/// <summary>US-076: cảnh báo ca trực (vắng, tự đóng, chờ bàn giao, lệch tiền) cho chủ bãi; bàn giao báo thêm người nhận.</summary>
public interface IShiftNotifications
{
    Task HandleShiftMissedAsync(ShiftMissed evt, Guid eventId, CancellationToken ct = default);
    Task HandleShiftAutoClosedAsync(ShiftAutoClosed evt, Guid eventId, CancellationToken ct = default);
    Task HandleHandoverPendingAsync(HandoverPending evt, Guid eventId, CancellationToken ct = default);
    Task HandleCashDiscrepancyAsync(CashDiscrepancyRaised evt, Guid eventId, CancellationToken ct = default);
}

public sealed class ShiftNotifications(IUserDirectory userDirectory, IUserNotificationSettingsPort settings, ITemplateRenderer renderer,
    ITransactionalNotificationSender sender) : IShiftNotifications
{
    public Task HandleShiftMissedAsync(ShiftMissed evt, Guid eventId, CancellationToken ct = default)
        => NotifyAsync(evt.OwnerProfileId, null, "SHIFT_MISSED", evt.ShiftId, evt.ParkingLotId, evt.StaffUserId,
            new() { ["StartAt"] = VietnamTime.Format(evt.ScheduledStartUtc), ["EndAt"] = VietnamTime.Format(evt.ScheduledEndUtc) }, eventId, ct);

    public Task HandleShiftAutoClosedAsync(ShiftAutoClosed evt, Guid eventId, CancellationToken ct = default)
        => NotifyAsync(evt.OwnerProfileId, null, "SHIFT_AUTO_CLOSED", evt.ShiftId, evt.ParkingLotId, evt.StaffUserId,
            new() { ["Reason"] = ReasonText(evt.Reason) }, eventId, ct);

    public Task HandleHandoverPendingAsync(HandoverPending evt, Guid eventId, CancellationToken ct = default)
        => NotifyAsync(evt.OwnerProfileId, evt.ReceiverStaffUserId, "SHIFT_HANDOVER_PENDING", evt.ShiftId, evt.ParkingLotId,
            evt.StaffUserId, [], eventId, ct);

    public Task HandleCashDiscrepancyAsync(CashDiscrepancyRaised evt, Guid eventId, CancellationToken ct = default)
        => NotifyAsync(evt.OwnerProfileId, null, "SHIFT_CASH_DISCREPANCY", evt.ShiftId, evt.ParkingLotId, evt.StaffUserId,
            new() { ["Amount"] = $"{evt.Difference:N0}đ" }, eventId, ct);

    private static string ReasonText(string reason) => reason switch
    {
        "Forgot" => "quên kết ca",
        "StaffRevoked" => "người trực bị gỡ khỏi bãi",
        "StaffSuspended" => "tài khoản người trực bị đình chỉ",
        "OwnerLocked" => "hồ sơ chủ bãi bị khóa",
        _ => reason,
    };

    private async Task NotifyAsync(int ownerProfileId, int? receiverUserId, string templateKey, int shiftId, int lotId, int staffUserId,
        Dictionary<string, string> placeholders, Guid eventId, CancellationToken ct)
    {
        placeholders["ShiftId"] = shiftId.ToString();
        placeholders["LotId"] = lotId.ToString();
        placeholders["StaffId"] = staffUserId.ToString();
        var owner = await userDirectory.GetOwnerUserIdAsync(ownerProfileId, ct);
        // Chủ bãi tự nhận bàn giao của chính ca kế tiếp chỉ nhận một thông báo.
        foreach (var userId in new[] { owner, receiverUserId }.OfType<int>().Where(id => id > 0).Distinct())
        {
            var language = await settings.GetLanguageAsync(userId, ct);
            var rendered = await renderer.RenderAsync(templateKey, NotificationChannel.InApp, language, placeholders, ct);
            await sender.SendAsync(userId, NotificationChannel.InApp, templateKey, rendered.Title, rendered.Body,
                System.Text.Json.JsonSerializer.Serialize(new { shiftId, lotId }), $"evt:{eventId}:{userId}", ct);
        }
    }
}
