using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

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

    // ===== Luồng duyệt (US-080): Draft → PendingReview → Approved → Paid; PendingReview → Draft khi bị từ chối =====

    public const int ReasonMaxLength = 500;
    public const int PayoutReferenceMaxLength = 100;
    /// <summary>Cho phép lệch đồng hồ khi nhập ngày chuyển khoản.</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>Gửi duyệt: chỉ kỳ Draft không bị chặn (đã nạp khoản giữ, đủ hoa hồng, booking xác minh được). Sai trạng thái → 409.</summary>
    public void Submit(int userId, DateTime nowUtc)
    {
        EnsureStatus(SettlementStatus.Draft, "gửi duyệt");
        if (IsBlocked)
            throw new ConflictException($"Kỳ quyết toán {Code} đang bị chặn ({BlockedReasons}) – hãy xử lý rồi tính lại trước khi gửi duyệt.");
        Status = SettlementStatus.PendingReview;
        SubmittedByUserId = userId;
        SubmittedAtUtc = nowUtc;
    }

    /// <summary>Duyệt (chốt kỳ): từ đây kỳ bất biến, sửa đổi chỉ qua FinancialAdjustment vào kỳ sau. Người gửi không tự duyệt khi bật 4 mắt → 403.</summary>
    public void Approve(int userId, DateTime nowUtc, bool requireDifferentApprover)
    {
        EnsureStatus(SettlementStatus.PendingReview, "duyệt");
        if (requireDifferentApprover && SubmittedByUserId == userId)
            throw new ForbiddenException($"Người gửi duyệt kỳ {Code} không được tự duyệt – cần một Admin khác.");
        Status = SettlementStatus.Approved;
        ApprovedByUserId = userId;
        ApprovedAtUtc = nowUtc;
    }

    /// <summary>Từ chối: PendingReview → Draft (lý do bắt buộc) để tính lại / xử lý rồi gửi lại.</summary>
    public void Reject(int userId, string? reason, DateTime nowUtc)
    {
        EnsureStatus(SettlementStatus.PendingReview, "từ chối");
        var text = RequireText(reason, "Lý do từ chối", ReasonMaxLength);
        Status = SettlementStatus.Draft;
        RejectedByUserId = userId;
        RejectedAtUtc = nowUtc;
        RejectReason = text;
    }

    /// <summary>
    /// Đã chuyển khoản: Approved → Paid, mã giao dịch ngân hàng bắt buộc, ngày chuyển không ở tương lai và không trước lúc duyệt.
    /// NetPayout ≤ 0 → không có khoản chi (số âm mang sang kỳ sau) → 409.
    /// </summary>
    public void MarkPaid(int userId, string? payoutReference, DateTime? paidAtUtc, DateTime nowUtc)
    {
        EnsureStatus(SettlementStatus.Approved, "đánh dấu đã chi");
        if (NetPayout <= 0)
            throw new ConflictException($"Kỳ quyết toán {Code} có thực chi {NetPayout:N0} ≤ 0 – không có khoản chuyển; số âm được mang sang kỳ sau.");
        var reference = RequireText(payoutReference, "Mã giao dịch chuyển khoản", PayoutReferenceMaxLength);
        if (paidAtUtc is not { } paidAt || paidAt == default) throw new ValidationException("Ngày chuyển khoản không được để trống.");
        if (paidAt > nowUtc + ClockSkew) throw new ValidationException("Ngày chuyển khoản không được ở tương lai.");
        if (ApprovedAtUtc is { } approvedAt && paidAt < approvedAt - ClockSkew)
            throw new ValidationException("Ngày chuyển khoản không được trước lúc duyệt kỳ.");
        Status = SettlementStatus.Paid;
        PaidByUserId = userId;
        PaidAtUtc = paidAt;
        PayoutReference = reference;
    }

    /// <summary>Thao tác được phép ở trạng thái hiện tại (giao diện bật / tắt nút).</summary>
    public IReadOnlyList<string> AllowedActions() => Status switch
    {
        SettlementStatus.Draft => IsBlocked ? ["Regenerate"] : ["Regenerate", "Submit"],
        SettlementStatus.PendingReview => ["Approve", "Reject"],
        SettlementStatus.Approved => NetPayout > 0 ? ["MarkPaid"] : [],
        _ => [],
    };

    private void EnsureStatus(SettlementStatus expected, string action)
    {
        if (Status != expected)
            throw new ConflictException($"Không thể {action} kỳ quyết toán {Code} đang ở trạng thái {Status} (cần {expected}).");
    }

    internal static string RequireText(string? value, string field, int maxLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) throw new ValidationException($"{field} không được để trống.");
        if (text.Length > maxLength) throw new ValidationException($"{field} tối đa {maxLength} ký tự.");
        return text;
    }

    /// <summary>Ghi kết quả tính (lần đầu hoặc tính lại). Chỉ Draft được tính lại – từ PendingReview trở đi bất biến.</summary>
    public void ApplyComputation(Services.SettlementComputation computation, DateTime nowUtc, int? userId, DateTime? holdsCapturedAtUtc)
    {
        if (Status != SettlementStatus.Draft)
            throw new ConflictException($"Kỳ quyết toán {Code} đang ở trạng thái {Status}, chỉ kỳ Draft được tính lại.");

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
