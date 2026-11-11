using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace ParkingService.Domain.Entities;

public enum PhysicalChangeScope
{
    Facility = 1, // Toàn bộ bãi xe
    Zone = 2,     // Một khu vực / tòa
    Floor = 3,    // Một tầng
    Slot = 4      // Một hoặc nhóm ô đỗ
}

public enum PhysicalChangeType
{
    LayoutRenovation = 1,  // Cải tạo mặt bằng / phân chia lại ô đỗ
    Maintenance = 2,       // Sửa chữa, nâng cấp kết cấu
    AmenityChange = 3,     // Bổ sung hoặc hủy bỏ tiện ích (trụ sạc EV, mái che...)
    CapacityChange = 4,    // Thay đổi số lượng chỗ đỗ
    Other = 5              // Thay đổi vật lý khác
}

public enum PhysicalChangeStatus
{
    InProgress = 1,    // Đang diễn ra, trong thời hạn đếm ngược 48 giờ
    Completed = 2,     // Hoàn thành cập nhật đúng hạn trong 48h
    Overdue = 3,       // Quá hạn 48 giờ mà chưa hoàn tất cập nhật sơ đồ
    CompletedLate = 4, // Hoàn tất cập nhật nhưng đã bị trễ hạn (> 48h)
    Cancelled = 5      // Đã hủy bỏ đợt thay đổi
}

/// <summary>
/// [ParkingService] Đợt thay đổi vật lý tại hiện trường bãi xe (US-061, EP09).
/// Tiêu chí 1: Đếm ngược 48 giờ và nhắc nhở cập nhật sơ đồ.
/// Tiêu chí 2: Quá 48h chưa cập nhật mà có khách đặt nhầm -> Chủ bãi chịu 100% bồi thường & ghi nhận vi phạm SLA Cấp 2.
/// </summary>
public class LotPhysicalChange : BaseEntity
{
    public int ParkingLotId { get; set; }
    public PhysicalChangeScope Scope { get; set; } = PhysicalChangeScope.Floor;
    public int? TargetId { get; set; }
    public string? AffectedSlotCodesJson { get; set; } // JSON array các mã slot bị ảnh hưởng, VD: ["A-01", "A-02"]
    public PhysicalChangeType ChangeType { get; set; } = PhysicalChangeType.LayoutRenovation;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Tiêu chí 1: Thời điểm bắt đầu thay đổi vật lý tại hiện trường.</summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>Tiêu chí 1: Hạn chót đếm ngược 48 giờ (= OccurredAtUtc + 48h).</summary>
    public DateTime DeadlineUtc { get; set; }

    public PhysicalChangeStatus Status { get; set; } = PhysicalChangeStatus.InProgress;

    public DateTime? CompletedAtUtc { get; set; }
    public int? RelatedLayoutVersionId { get; set; }
    public string? CompletionNote { get; set; }

    /// <summary>Tiêu chí 1: Theo dõi số lần và thời điểm gửi nhắc nhở cập nhật sơ đồ.</summary>
    public int ReminderCount { get; set; }
    public DateTime? LastRemindedAtUtc { get; set; }

    /// <summary>Tiêu chí 2: Ghi nhận sự cố khách đặt nhầm khi quá 48h chưa cập nhật sơ đồ.</summary>
    public bool HasBookingIncident { get; set; }
    public int BookingIncidentCount { get; set; }
    public SanctionLevel? ViolationLevel { get; set; }
    public decimal TotalCompensationAmount { get; set; }
    public int OwnerLiabilityPercent { get; set; } // Luôn = 100 khi vi phạm quá 48h

    public ParkingLot ParkingLot { get; set; } = null!;
    public LayoutVersion? RelatedLayoutVersion { get; set; }

    // ==================== DOMAIN METHODS ====================

    /// <summary>Ghi nhận một lần gửi nhắc nhở cập nhật sơ đồ cho chủ bãi.</summary>
    public void RecordReminder(DateTime remindedAtUtc)
    {
        ReminderCount++;
        LastRemindedAtUtc = remindedAtUtc;
    }

    /// <summary>Hoàn tất cập nhật sơ đồ (phân loại đúng hạn Completed vs trễ hạn CompletedLate).</summary>
    public void Complete(DateTime completedAtUtc, int? layoutVersionId, string? completionNote)
    {
        if (Status == PhysicalChangeStatus.Completed || Status == PhysicalChangeStatus.CompletedLate)
            throw new ConflictException("Đợt thay đổi vật lý này đã được hoàn tất trước đó.");
        if (Status == PhysicalChangeStatus.Cancelled)
            throw new ConflictException("Không thể hoàn tất đợt thay đổi vật lý đã bị hủy.");

        CompletedAtUtc = completedAtUtc;
        RelatedLayoutVersionId = layoutVersionId;
        CompletionNote = string.IsNullOrWhiteSpace(completionNote) ? null : completionNote.Trim();

        // Đánh giá SLA 48h: Trong hạn -> Completed, Quá hạn -> CompletedLate
        Status = completedAtUtc <= DeadlineUtc ? PhysicalChangeStatus.Completed : PhysicalChangeStatus.CompletedLate;
    }

    /// <summary>Kiểm tra và đánh dấu quá hạn 48 giờ.</summary>
    public bool CheckAndMarkOverdue(DateTime nowUtc)
    {
        if (Status == PhysicalChangeStatus.InProgress && nowUtc > DeadlineUtc)
        {
            Status = PhysicalChangeStatus.Overdue;
            return true;
        }
        return false;
    }

    /// <summary>Tiêu chí 2: Ghi nhận sự cố khách đặt nhầm ô khi quá 48h chưa cập nhật sơ đồ.</summary>
    public void RecordBookingIncident(string bookingCode, decimal compensationAmount)
    {
        if (Status != PhysicalChangeStatus.Overdue && Status != PhysicalChangeStatus.CompletedLate)
            throw new ConflictException("Chỉ áp dụng chế tài Tiêu chí 2 khi đợt thay đổi vật lý đã quá 48 giờ chưa cập nhật.");

        HasBookingIncident = true;
        BookingIncidentCount++;
        ViolationLevel = SanctionLevel.RankingPenalty; // Vi phạm SLA Cấp 2
        OwnerLiabilityPercent = 100;                    // Chủ bãi chịu 100% bồi thường
        TotalCompensationAmount += compensationAmount;
    }

    /// <summary>Hủy đợt thay đổi vật lý nếu hoãn thi công.</summary>
    public void Cancel(string? reason)
    {
        if (Status != PhysicalChangeStatus.InProgress && Status != PhysicalChangeStatus.Overdue)
            throw new ConflictException($"Không thể hủy đợt thay đổi đang ở trạng thái {Status}.");
        Status = PhysicalChangeStatus.Cancelled;
        CompletionNote = string.IsNullOrWhiteSpace(reason) ? "Đã hủy bởi chủ bãi." : reason.Trim();
    }
}
