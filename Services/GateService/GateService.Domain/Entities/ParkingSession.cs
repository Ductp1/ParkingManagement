using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Domain.Entities;

/// <summary>
/// [GateService] Một lượt xe vào/ra thực tế tại cổng (có booking hoặc khách vãng lai). Module: TV7.
/// Kế thừa từ ParkingSession của project Smart_Parking_System. Một biển số chỉ có tối đa 1 lượt Active/bãi.
/// Khách vãng lai không có tài khoản → BookingId, UserId, VehicleId để null, chỉ có PlateNumber.
/// </summary>
public class ParkingSession : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    /// <summary>→ ParkingService (không FK).</summary>
    public int ParkingLotId { get; set; }
    /// <summary>→ UserService (không FK).</summary>
    public int OwnerProfileId { get; set; }
    /// <summary>→ BookingService (không FK).</summary>
    public int? BookingId { get; set; }
    public string? BookingCode { get; set; }
    public int? UserId { get; set; }
    public int? VehicleId { get; set; }
    /// <summary>→ ParkingService (không FK).</summary>
    public int? SlotId { get; set; }
    public string? SlotCode { get; set; }

    /// <summary>Biển số chuẩn hóa "51F12345".</summary>
    public string PlateNumber { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; } = VehicleType.Sedan;
    public bool IsWalkIn { get; set; }

    // ===== Vào =====
    public DateTime EntryAtUtc { get; set; }
    public GateMethod CheckInMethod { get; set; }
    public string? EntryImagePath { get; set; }
    public string? EntryOcrRaw { get; set; }
    public double? EntryOcrConfidence { get; set; }
    public int? CheckedInByStaffId { get; set; }

    // ===== Ra =====
    public DateTime? ExitAtUtc { get; set; }
    public GateMethod? CheckOutMethod { get; set; }
    public string? ExitImagePath { get; set; }
    public string? ExitOcrRaw { get; set; }
    public double? ExitOcrConfidence { get; set; }
    public int? CheckedOutByStaffId { get; set; }

    public decimal? Fee { get; set; }
    public decimal? OverstayFee { get; set; }
    public ParkingSessionStatus Status { get; set; } = ParkingSessionStatus.Active;


    public ICollection<GateEvent> Events { get; set; } = new List<GateEvent>();
    /// <summary>Thu phí tại cổng ra (vãng lai, phụ thu quá giờ).</summary>
}
