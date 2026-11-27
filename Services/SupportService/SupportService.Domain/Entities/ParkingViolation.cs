using ParkingManagement.SharedKernel.Domain;

namespace SupportService.Domain.Entities;

/// <summary>
/// [SupportService] Biên bản xe đậu sai chỗ (chiếm chỗ người khác đã đặt) lập khi chủ bãi xử lý khiếu nại (US-090).
/// Mức phạt chép từ <see cref="LotFineRate"/> tại thời điểm lập; GateService/PaymentService thu khi xe ra cổng.
/// </summary>
public class ParkingViolation : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public int ComplaintId { get; set; }
    public int ParkingLotId { get; set; }
    public int? OwnerProfileId { get; set; }
    /// <summary>Biển số đã chuẩn hóa của xe vi phạm.</summary>
    public string PlateNumber { get; set; } = string.Empty;
    /// <summary>Chỗ bị chiếm (chỗ của booking bị khiếu nại).</summary>
    public string? SlotCode { get; set; }
    public decimal FineAmount { get; set; }
    public int? IssuedByUserId { get; set; }

    public Complaint Complaint { get; set; } = null!;
}
