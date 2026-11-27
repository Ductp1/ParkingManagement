using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Domain.Entities;

/// <summary>
/// [GateService] Ca trực của một người tại một bãi (US-076, US-077): lịch do chủ bãi xếp, giờ thực do người trực bấm Start / End.
/// Nhiều ca chồng giờ dùng chung một két tiền, chỉ ca IsCashHolder được thu tiền mặt.
/// </summary>
public class Shift : BaseEntity
{
    /// <summary>→ ParkingService (không FK).</summary>
    public int ParkingLotId { get; set; }
    /// <summary>→ UserService (không FK).</summary>
    public int OwnerProfileId { get; set; }
    public int StaffUserId { get; set; }
    public DateTime ScheduledStartUtc { get; set; }
    public DateTime ScheduledEndUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Scheduled;
    /// <summary>Ngày (giờ Việt Nam) lúc ca bắt đầu theo lịch – báo cáo gộp theo ngày này, kể cả ca qua đêm.</summary>
    public DateOnly BusinessDate { get; set; }
    public int? TemplateId { get; set; }
    public int CreatedByUserId { get; set; }
    public ShiftClosedReason? ClosedReason { get; set; }
    public bool IsFlagged { get; set; }

    // ===== Tiền mặt =====
    public bool IsCashHolder { get; set; } = true;
    /// <summary>Tổng tiền mặt PaymentService ghi cho ca; null = chưa lấy được.</summary>
    public decimal? CashSystem { get; set; }
    /// <summary>Người giao đếm khi End.</summary>
    public decimal? CashCounted { get; set; }
    /// <summary>Người nhận đếm lại khi Accept.</summary>
    public decimal? CashReceived { get; set; }
    public ShiftDiscrepancyStatus? DiscrepancyStatus { get; set; }
    public string? DiscrepancyNote { get; set; }
    /// <summary>Ca nhận bàn giao; null = chủ bãi nhận.</summary>
    public int? HandoverToShiftId { get; set; }
    public int? HandoverAcceptedByUserId { get; set; }
    public DateTime? HandoverAcceptedAtUtc { get; set; }

    public int CheckInCount { get; set; }
    public int CheckOutCount { get; set; }
    public int NoShowCancelCount { get; set; }
    public int ManualExceptionCount { get; set; }
    public decimal CashCollected { get; set; }
    public string? HandoverNote { get; set; }
    /// <summary>xmin của PostgreSQL – chặn hai thao tác ghi đè nhau.</summary>
    public uint Version { get; set; }

    public ICollection<GateEvent> Events { get; set; } = new List<GateEvent>();

    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    public static DateOnly BusinessDateOf(DateTime startUtc) => DateOnly.FromDateTime(startUtc + VietnamOffset);
    public static DateTime StartOfBusinessDateUtc(DateOnly date) => DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue) - VietnamOffset, DateTimeKind.Utc);
}
