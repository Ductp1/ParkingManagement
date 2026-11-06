using ParkingManagement.SharedKernel.Domain;

namespace GateService.Domain.Entities;

/// <summary>
/// [GateService] Cache trạng thái vận hành của bãi (US-069) — cập nhật bởi consumer
/// LotEmergencyStopped/Lifted từ ParkingService. Check-in (vãng lai + booking QR) đọc bảng này
/// để CHẶN xe mới vào khi bãi đóng khẩn cấp. Check-out không check — xe trong bãi thoát an toàn.
/// </summary>
public class LotOperationalStatus : BaseEntity
{
    public int ParkingLotId { get; set; }
    public bool IsClosed { get; set; }
    public string? ClosedReason { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
}