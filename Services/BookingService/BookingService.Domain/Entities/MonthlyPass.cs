using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace BookingService.Domain.Entities;

/// <summary>
/// [BookingService] Vé tháng với Dedicated Slot gán cố định cho 1 biển số (US-111, UC-55, Đặc tả v3 §3.3 – Phase 2).
/// Tài xế phải KYC trước khi mua. Check-in bằng biển số/QR, không tính phí lượt.
/// </summary>
public class MonthlyPass : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    /// <summary>→ UserService / VehicleService / ParkingService (không FK).</summary>
    public int UserId { get; set; }
    public int VehicleId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public int ParkingLotId { get; set; }
    public int OwnerProfileId { get; set; }
    /// <summary>Slot cố định (Dedicated Slot); null = chỉ đảm bảo có chỗ trong bãi.</summary>
    public int? SlotId { get; set; }
    public string? SlotCode { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }
    public decimal Price { get; set; }
    public bool AutoRenew { get; set; }
    public MonthlyPassStatus Status { get; set; } = MonthlyPassStatus.PendingPayment;
    public int? PaymentId { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
}
