using ParkingManagement.SharedKernel.Domain;

namespace SupportService.Domain.Entities;

/// <summary>[SupportService] Mức phạt đậu sai chỗ của 1 bãi – chủ bãi tự đặt (US-090). Mỗi bãi 1 dòng.</summary>
public class LotFineRate : BaseEntity
{
    /// <summary>→ ParkingService (không FK).</summary>
    public int ParkingLotId { get; set; }
    /// <summary>→ UserService (không FK). Chủ bãi đặt mức phạt.</summary>
    public int OwnerProfileId { get; set; }
    /// <summary>Phạt mỗi lần xe đậu vào chỗ người khác đã đặt.</summary>
    public decimal WrongSlotFine { get; set; }
    public int? UpdatedByUserId { get; set; }
}
