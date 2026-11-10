using ParkingManagement.SharedKernel.Domain;

namespace PaymentService.Domain.Entities;

/// <summary>Trạng thái duyệt bút toán điều chỉnh (US-080 AC3: có lý do và người duyệt).</summary>
public enum FinancialAdjustmentStatus { PendingApproval = 0, Approved = 1, Rejected = 2 }

/// <summary>Nguồn phát sinh bút toán điều chỉnh.</summary>
public enum FinancialAdjustmentSource
{
    /// <summary>Admin tạo tay (refund, chargeback, sửa hoa hồng, bồi thường…).</summary>
    Manual = 0,
    /// <summary>Voucher đền bù tính cho chủ bãi (CompensationVoucher.ChargedToOwnerProfileId) – hệ thống tự tạo khi sinh kỳ.</summary>
    CompensationVoucher = 1,
    /// <summary>Tiền phạt theo chế tài SLA (US-097) – SourceReference = mã chế tài.</summary>
    Sanction = 2,
}

/// <summary>
/// [PaymentService] Bút toán bù trừ (FLOW 7, US-080 AC3). Số âm = trừ chủ bãi, dương = cộng cho chủ bãi. CHỈ THÊM: không sửa số tiền / xóa,
/// đảo bằng bút toán ngược (ReversesAdjustmentId). Chỉ bút toán Approved mới được gắn vào kỳ (SettlementId) khi sinh / tính lại kỳ Draft.
/// </summary>
public class FinancialAdjustment : BaseEntity
{
    /// <summary>→ UserService (không FK).</summary>
    public int OwnerProfileId { get; set; }
    public int? SettlementId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }

    public FinancialAdjustmentStatus Status { get; set; } = FinancialAdjustmentStatus.PendingApproval;
    public FinancialAdjustmentSource SourceType { get; set; } = FinancialAdjustmentSource.Manual;
    /// <summary>Mã nguồn: mã voucher (đảo: "{mã}:REVOKED"), mã chế tài…</summary>
    public string? SourceReference { get; set; }
    public int? ReversesAdjustmentId { get; set; }
    /// <summary>null khi hệ thống tự duyệt (voucher đền bù).</summary>
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public int? RejectedByUserId { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public string? RejectReason { get; set; }

    /// <summary>Optimistic concurrency (ServiceDefaults đóng dấu mỗi lần lưu).</summary>
    public byte[] RowVersion { get; set; } = [];

    public Settlement? Settlement { get; set; }

    public const string RevokedSuffix = ":REVOKED";

    /// <summary>Bút toán trừ chủ bãi giá trị voucher đền bù (lỗi do bãi) – hệ thống tự duyệt.</summary>
    public static FinancialAdjustment ForCompensationVoucher(int ownerProfileId, string voucherCode, decimal amount, int issuedByUserId,
        DateTime nowUtc)
        => new()
        {
            OwnerProfileId = ownerProfileId,
            Amount = -amount,
            Reason = $"CompensationVoucher {voucherCode}",
            CreatedByUserId = issuedByUserId,
            Status = FinancialAdjustmentStatus.Approved,
            SourceType = FinancialAdjustmentSource.CompensationVoucher,
            SourceReference = voucherCode,
            ApprovedAtUtc = nowUtc,
            CreatedAtUtc = nowUtc,
        };

    /// <summary>Đảo bút toán voucher khi voucher bị thu hồi sau khi đã trừ chủ bãi: cộng lại <paramref name="chargedAmount"/> (dương).</summary>
    public static FinancialAdjustment ReverseCompensationVoucher(int ownerProfileId, string voucherCode, decimal chargedAmount,
        int? chargedAdjustmentId, int issuedByUserId, DateTime nowUtc)
        => new()
        {
            OwnerProfileId = ownerProfileId,
            Amount = chargedAmount,
            Reason = $"CompensationVoucher {voucherCode} revoked",
            CreatedByUserId = issuedByUserId,
            Status = FinancialAdjustmentStatus.Approved,
            SourceType = FinancialAdjustmentSource.CompensationVoucher,
            SourceReference = voucherCode + RevokedSuffix,
            ReversesAdjustmentId = chargedAdjustmentId,
            ApprovedAtUtc = nowUtc,
            CreatedAtUtc = nowUtc,
        };
}
