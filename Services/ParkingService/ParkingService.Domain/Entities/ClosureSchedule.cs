using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace ParkingService.Domain.Entities;

/// <summary>
/// [ParkingService] Lịch tạm đóng 1 phần hoặc toàn bộ bãi: bảo trì, sự kiện, ngày lễ, Emergency Stop (US-063, US-069, UC-29, UC-37).
/// TargetType + TargetId trỏ tới Lot / Zone / Floor / Slot. Trong khoảng thời gian này không nhận booking mới cho phần bị đóng.
/// </summary>
public class ClosureSchedule : BaseEntity
{
    public int ParkingLotId { get; set; }
    public ClosureTargetType TargetType { get; set; }
    /// <summary>Id của Zone/Floor/Slot; bằng ParkingLotId nếu đóng cả bãi.</summary>
    public int TargetId { get; set; }
    public DateTime StartsAtUtc { get; set; }
    /// <summary>null = đóng đến khi mở lại thủ công (Emergency Stop).</summary>
    public DateTime? EndsAtUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool IsEmergency { get; set; }
    /// <summary>Đã báo cho tài xế có booking bị ảnh hưởng chưa.</summary>
    public bool AffectedBookingsNotified { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime? CancelledAtUtc { get; set; }

    public ParkingLot ParkingLot { get; set; } = null!;
}
