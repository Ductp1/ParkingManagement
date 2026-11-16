using ParkingManagement.SharedKernel.Domain;

namespace ParkingService.Domain.Entities;

/// <summary>
/// [ParkingService] Nhật ký audit Emergency Stop (US-069): mỗi lần đóng/mở khẩn cấp 1 dòng,
/// phục vụ điều tra sự cố và hiển thị lịch sử trong dashboard chủ bãi/admin.
/// </summary>
public class EmergencyStopLog : BaseEntity
{
    public int ParkingLotId { get; set; }

    /// <summary>"Stop" (đóng bãi khẩn cấp) hoặc "Lift" (gỡ đóng).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>UserId của người thao tác (chủ bãi/nhân viên/admin).</summary>
    public int ActorUserId { get; set; }

    /// <summary>Vai trò người thao tác: LotOwner | Staff | Admin | System.</summary>
    public string ActorRole { get; set; } = string.Empty;

    /// <summary>Lý do đóng/gỡ – bắt buộc ≥ 5 ký tự khi Stop.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Số booking bị ảnh hưởng tại thời điểm đóng (luôn 0 với Lift).</summary>
    public int AffectedBookings { get; set; }
}