using ParkingManagement.SharedKernel.Domain;

namespace PaymentService.Domain.Entities;

/// <summary>[PaymentService] Bút toán bù trừ khi đối soát lệch (FLOW 7). Số âm = trừ chủ bãi.</summary>
public class FinancialAdjustment : BaseEntity
{
    /// <summary>→ UserService (không FK).</summary>
    public int OwnerProfileId { get; set; }
    public int? SettlementId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }

    public Settlement? Settlement { get; set; }
}
