using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>
/// [PaymentService] Kỳ quyết toán tuần cho 1 chủ bãi (UC-43, FLOW 7). Module: TV8.
/// NetPayout = Gross − Commission − Refund − Held + Adjustment.
/// </summary>
public class Settlement : BaseEntity
{
    /// <summary>→ UserService (không FK).</summary>
    public int OwnerProfileId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal GrossAmount { get; set; }
    /// <summary>Tỷ lệ hoa hồng áp dụng kỳ này (mặc định 0.10).</summary>
    public decimal CommissionRate { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal RefundAmount { get; set; }
    /// <summary>Tiền bị giữ do đang tranh chấp.</summary>
    public decimal HeldAmount { get; set; }
    public decimal AdjustmentAmount { get; set; }
    public decimal NetPayout { get; set; }
    public SettlementStatus Status { get; set; } = SettlementStatus.Draft;
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public string? PayoutReference { get; set; }

    public ICollection<SettlementLine> Lines { get; set; } = new List<SettlementLine>();
    public ICollection<FinancialAdjustment> Adjustments { get; set; } = new List<FinancialAdjustment>();
}
