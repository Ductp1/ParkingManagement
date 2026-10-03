using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>[PaymentService] Dòng chi tiết của kỳ quyết toán (mỗi giao dịch / hoàn tiền / khoản giữ).</summary>
public class SettlementLine : BaseEntity
{
    public int SettlementId { get; set; }
    public SettlementLineType LineType { get; set; }
    public int? PaymentId { get; set; }
    public int? RefundId { get; set; }
    /// <summary>→ ParkingService (không FK).</summary>
    public int ParkingLotId { get; set; }
    public string? ReferenceCode { get; set; }
    public decimal Amount { get; set; }
    public decimal CommissionAmount { get; set; }

    public Settlement Settlement { get; set; } = null!;
    public Payment? Payment { get; set; }
    public Refund? Refund { get; set; }
}
