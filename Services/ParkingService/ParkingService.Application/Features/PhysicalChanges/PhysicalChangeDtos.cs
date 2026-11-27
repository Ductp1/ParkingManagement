using System.Text.Json;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.PhysicalChanges;

/// <summary>
/// DTO chi tiết đợt thay đổi vật lý của bãi xe (US-061, EP09).
/// </summary>
public sealed record LotPhysicalChangeDto(
    int Id,
    int ParkingLotId,
    string Scope,
    int? TargetId,
    IReadOnlyList<string> AffectedSlotCodes,
    string ChangeType,
    string Title,
    string? Description,
    DateTime OccurredAtUtc,
    DateTime DeadlineUtc,
    string Status,
    bool IsOverdue,
    double RemainingHours,
    int ReminderCount,
    DateTime? LastRemindedAtUtc,
    bool HasBookingIncident,
    int BookingIncidentCount,
    string? ViolationLevel,
    int OwnerLiabilityPercent,
    decimal TotalCompensationAmount,
    DateTime? CompletedAtUtc,
    int? RelatedLayoutVersionId,
    string? CompletionNote,
    DateTime CreatedAtUtc);

/// <summary>Yêu cầu khai báo đợt thay đổi vật lý tại hiện trường (Tiêu chí 1).</summary>
public sealed record DeclarePhysicalChangeRequest(
    string Scope,
    int? TargetId,
    IReadOnlyList<string>? AffectedSlotCodes,
    string ChangeType,
    string Title,
    string? Description,
    DateTime? OccurredAtUtc = null);

/// <summary>Yêu cầu hoàn tất cập nhật sơ đồ cho đợt thay đổi vật lý.</summary>
public sealed record CompletePhysicalChangeRequest(
    int? RelatedLayoutVersionId,
    string? CompletionNote);

/// <summary>Yêu cầu hủy đợt thay đổi vật lý nếu hoãn thi công.</summary>
public sealed record CancelPhysicalChangeRequest(string? Reason);

/// <summary>Yêu cầu ghi nhận sự cố khách đặt nhầm ô do quá 48h chưa cập nhật sơ đồ (Tiêu chí 2).</summary>
public sealed record RecordPhysicalChangeBookingErrorRequest(
    string BookingCode,
    string? SlotCode,
    decimal CompensationAmount,
    string Reason);

/// <summary>Kết quả gửi thông báo nhắc nhở cập nhật sơ đồ (Tiêu chí 1).</summary>
public sealed record PhysicalChangeReminderResultDto(
    int ChangeId,
    int ParkingLotId,
    int ReminderCount,
    DateTime LastRemindedAtUtc,
    double RemainingHours,
    string Message);

/// <summary>Báo cáo tóm tắt tình trạng tuân thủ SLA 48h và vi phạm Cấp 2 của bãi xe.</summary>
public sealed record PhysicalChangeSlaSummaryDto(
    int ParkingLotId,
    int TotalChanges,
    int InProgressCount,
    int CompletedOnTimeCount,
    int OverdueCount,
    int CompletedLateCount,
    int TotalBookingIncidents,
    int Level2ViolationsCount,
    decimal TotalCompensationPaidByOwner,
    double SlaComplianceRate);

public static class PhysicalChangeMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static LotPhysicalChangeDto ToDto(this LotPhysicalChange change, DateTime nowUtc)
    {
        var isFinished = change.Status is PhysicalChangeStatus.Completed
            or PhysicalChangeStatus.CompletedLate
            or PhysicalChangeStatus.Cancelled;

        var isOverdue = change.Status == PhysicalChangeStatus.Overdue
            || (change.Status == PhysicalChangeStatus.InProgress && nowUtc > change.DeadlineUtc);

        var remainingHours = isFinished
            ? 0
            : Math.Max(0, Math.Round((change.DeadlineUtc - nowUtc).TotalHours, 1));

        IReadOnlyList<string> affectedSlots = [];
        if (!string.IsNullOrWhiteSpace(change.AffectedSlotCodesJson))
        {
            try
            {
                affectedSlots = JsonSerializer.Deserialize<List<string>>(change.AffectedSlotCodesJson, JsonOptions) ?? [];
            }
            catch
            {
                affectedSlots = [];
            }
        }

        return new LotPhysicalChangeDto(
            change.Id,
            change.ParkingLotId,
            change.Scope.ToString(),
            change.TargetId,
            affectedSlots,
            change.ChangeType.ToString(),
            change.Title,
            change.Description,
            change.OccurredAtUtc,
            change.DeadlineUtc,
            change.Status.ToString(),
            isOverdue,
            remainingHours,
            change.ReminderCount,
            change.LastRemindedAtUtc,
            change.HasBookingIncident,
            change.BookingIncidentCount,
            change.ViolationLevel?.ToString(),
            change.OwnerLiabilityPercent,
            change.TotalCompensationAmount,
            change.CompletedAtUtc,
            change.RelatedLayoutVersionId,
            change.CompletionNote,
            change.CreatedAtUtc);
    }
}
