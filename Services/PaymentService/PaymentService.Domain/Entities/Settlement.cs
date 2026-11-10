using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>
/// [PaymentService] Kỳ quyết toán của 1 chủ bãi theo chu kỳ SETTLEMENT_CYCLE (UC-43, FLOW 7, US-080). Module: TV8.
/// NetPayout = Gross − Refund − OwnerCollected − Commission − Held + Adjustment + CarryOver:
/// <list type="bullet">
/// <item>Gross / Refund: mọi phương thức; OwnerCollected = tiền chủ bãi tự thu (Cash / VietQR vào tài khoản chủ bãi) sau hoàn – nền tảng
///   không giữ khoản này nên không chi lại, nhưng hoa hồng của nó vẫn bị trừ.</item>
/// <item>Commission = Σ hoa hồng các dòng (Accrual của giao dịch mới, phần giảm hoa hồng của hoàn tiền là số âm).</item>
/// <item>Held = khoản giữ mới do tranh chấp − khoản giải phóng (số dương = đang giữ thêm).</item>
/// <item>Adjustment = Σ FinancialAdjustment đã duyệt gắn vào kỳ; CarryOver = NetPayout âm của kỳ trước mang sang.</item>
/// </list>
/// NetPayout ≤ 0 → không chuyển tiền; số âm được mang sang kỳ sau. Từ PendingReview trở đi kỳ bất biến – mọi sửa đổi qua FinancialAdjustment.
/// </summary>
public class Settlement : BaseEntity
{
    /// <summary>Mã kỳ duy nhất: STL-{yyyyMMdd ngày đầu kỳ}-{chủ bãi} (AC1 "mã riêng").</summary>
    public string Code { get; set; } = string.Empty;
    public int? SettlementPeriodId { get; set; }
    /// <summary>→ UserService (không FK).</summary>
    public int OwnerProfileId { get; set; }
    public DateOnly PeriodStart { get; set; }
    /// <summary>Ngày cuối kỳ (bao gồm).</summary>
    public DateOnly PeriodEnd { get; set; }
    public decimal GrossAmount { get; set; }
    /// <summary>Tỷ lệ hoa hồng HIỆU DỤNG của kỳ = Commission / Gross (chỉ hiển thị; mỗi giao dịch giữ tỷ lệ riêng theo US-079).</summary>
    public decimal CommissionRate { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal RefundAmount { get; set; }
    /// <summary>Tiền chủ bãi đã tự thu (Cash / VietQR) sau hoàn – trừ khỏi khoản nền tảng phải chi.</summary>
    public decimal OwnerCollectedAmount { get; set; }
    /// <summary>Tiền bị giữ do đang tranh chấp (giữ mới − giải phóng).</summary>
    public decimal HeldAmount { get; set; }
    public decimal AdjustmentAmount { get; set; }
    /// <summary>NetPayout âm của kỳ trước được mang sang (≤ 0).</summary>
    public decimal CarryOverAmount { get; set; }
    public int? CarryOverFromSettlementId { get; set; }
    public decimal NetPayout { get; set; }
    public SettlementStatus Status { get; set; } = SettlementStatus.Draft;

    /// <summary>Còn điều kiện chưa đủ để gửi duyệt (BlockedReasons, phân tách bằng dấu phẩy).</summary>
    public bool IsBlocked { get; set; }
    public string? BlockedReasons { get; set; }
    /// <summary>Thời điểm (lại) tính kỳ; GeneratedByUserId = null khi do job.</summary>
    public DateTime GeneratedAtUtc { get; set; }
    public int? GeneratedByUserId { get; set; }
    /// <summary>Lúc lấy danh sách khoản giữ từ SupportService (null = chưa lấy được – job không có JWT Admin).</summary>
    public DateTime? HoldsCapturedAtUtc { get; set; }

    public int? SubmittedByUserId { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public int? RejectedByUserId { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public string? RejectReason { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public int? PaidByUserId { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public string? PayoutReference { get; set; }

    /// <summary>Optimistic concurrency (ServiceDefaults đóng dấu mỗi lần lưu): hai Admin thao tác cùng kỳ → người sau 409.</summary>
    public byte[] RowVersion { get; set; } = [];

    public SettlementPeriod? Period { get; set; }
    public ICollection<SettlementLine> Lines { get; set; } = new List<SettlementLine>();
    public ICollection<FinancialAdjustment> Adjustments { get; set; } = new List<FinancialAdjustment>();

    /// <summary>Kỳ mới ở trạng thái Draft (job hoặc Admin sinh).</summary>
    public static Settlement NewDraft(SettlementPeriod period, int ownerProfileId)
        => new()
        {
            Code = Services.SettlementCalendar.SettlementCode(period.PeriodStart, ownerProfileId),
            SettlementPeriodId = period.Id,
            Period = period.Id == 0 ? period : null,
            OwnerProfileId = ownerProfileId,
            PeriodStart = period.PeriodStart,
            PeriodEnd = period.PeriodEnd,
            Status = SettlementStatus.Draft,
        };

    /// <summary>Ghi kết quả tính (lần đầu hoặc tính lại). Chỉ Draft được tính lại – từ PendingReview trở đi bất biến.</summary>
    public void ApplyComputation(Services.SettlementComputation computation, DateTime nowUtc, int? userId, DateTime? holdsCapturedAtUtc)
    {
        if (Status != SettlementStatus.Draft)
            throw new InvalidOperationException($"Kỳ quyết toán {Code} đang ở trạng thái {Status}, chỉ kỳ Draft được tính lại.");

        GrossAmount = computation.Gross;
        RefundAmount = computation.Refund;
        OwnerCollectedAmount = computation.OwnerCollected;
        CommissionAmount = computation.Commission;
        CommissionRate = computation.EffectiveCommissionRate;
        HeldAmount = computation.Held;
        AdjustmentAmount = computation.Adjustment;
        CarryOverAmount = computation.CarryOver;
        CarryOverFromSettlementId = computation.CarryOverFromSettlementId;
        NetPayout = computation.NetPayout;
        IsBlocked = computation.BlockedReasons.Count > 0;
        BlockedReasons = IsBlocked ? string.Join(",", computation.BlockedReasons) : null;
        GeneratedAtUtc = nowUtc;
        GeneratedByUserId = userId;
        HoldsCapturedAtUtc = holdsCapturedAtUtc;
        if (Id == 0) CreatedAtUtc = nowUtc;

        foreach (var line in computation.Lines)
            Lines.Add(new SettlementLine
            {
                LineType = line.LineType,
                PaymentId = line.PaymentId,
                RefundId = line.RefundId,
                ParkingLotId = line.ParkingLotId,
                ReferenceCode = line.ReferenceCode,
                Amount = line.Amount,
                CommissionAmount = line.CommissionAmount,
                IsOwnerCollected = line.IsOwnerCollected,
                OccurredAtUtc = line.OccurredAtUtc,
                BlockedReason = line.BlockedReason,
            });
    }
}
