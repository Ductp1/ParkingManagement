using ParkingManagement.SharedKernel.Domain;

namespace ParkingService.Domain.Entities;

/// <summary>
/// [ParkingService] Cấu hình sức chứa & kết nối của bãi (Đặc tả v3 §4.3, Kiến trúc v3 §2.2). Module: TV6.
/// Quan hệ 1-1 với ParkingLot.
/// </summary>
public class LotCapacityConfig : BaseEntity
{
    public int ParkingLotId { get; set; }
    /// <summary>Số chỗ tối đa mở bán trên App.</summary>
    public int OnlineQuota { get; set; }
    /// <summary>% giữ cho khách vãng lai (bãi Mức 0: 30–50%).</summary>
    public int WalkInBufferPercent { get; set; } = 30;
    /// <summary>Còn ≤ ngưỡng này thì khóa kênh online.</summary>
    public int OnlineLockThreshold { get; set; } = 5;
    public bool InstantBookingPaused { get; set; }
    public bool IsEmergencyStopped { get; set; }
    public string? EmergencyReason { get; set; }

    // Heartbeat: 60 giây/lần, 3 lần lỗi liên tiếp → Stale
    public DateTime? LastHeartbeatAtUtc { get; set; }
    public int ConsecutiveHeartbeatFailures { get; set; }
    public bool IsStale { get; set; }

    public ParkingLot ParkingLot { get; set; } = null!;
}
