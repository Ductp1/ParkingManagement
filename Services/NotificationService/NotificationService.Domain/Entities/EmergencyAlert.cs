using ParkingManagement.SharedKernel.Domain;

namespace NotificationService.Domain.Entities;

/// <summary>
/// [NotificationService] 1 đợt thông báo khẩn cấp (US-086) — metadata của 1 fan-out khi bãi
/// đóng khẩn cấp. Trạng thái gửi CHI TIẾT theo (người nhận × kênh) nằm ở bảng Notifications
/// (cùng BatchKey); bảng này chỉ giữ định danh đợt để API tổng hợp + phân quyền chủ bãi.
/// </summary>
public class EmergencyAlert : BaseEntity
{
    /// <summary>Key nhóm thông báo — "evt:{EventId}" — trùng với Notification.BatchKey. Unique.</summary>
    public string BatchKey { get; set; } = string.Empty;
    /// <summary>→ ParkingService (không FK).</summary>
    public int ParkingLotId { get; set; }
    public string LotName { get; set; } = string.Empty;
    /// <summary>Chủ bãi — anchor phân quyền: owner chỉ xem đợt của bãi mình.</summary>
    public int OwnerProfileId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime StoppedAtUtc { get; set; }
    /// <summary>Số người nhận (khách bị ảnh hưởng + chủ bãi) — số row Notifications có thể lớn hơn (đa kênh).</summary>
    public int TotalRecipients { get; set; }
    /// <summary>Người bấm đóng bãi (audit từ US-069).</summary>
    public int StoppedByUserId { get; set; }
}
