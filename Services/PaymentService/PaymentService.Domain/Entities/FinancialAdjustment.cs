using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;

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
    public const int ReasonMaxLength = 500;
    /// <summary>Chặn nhập nhầm số (VND): |Amount| ≤ 10 tỷ.</summary>
    public const decimal MaxAbsoluteAmount = 10_000_000_000m;

    /// <summary>
    /// Admin tạo bút toán (chờ duyệt). Amount ≠ 0, là số nguyên VND; lý do bắt buộc. sanctionId → nguồn Sanction (US-097);
    /// <paramref name="reverses"/> = bút toán Approved cần đảo (cùng chủ bãi, số tiền ngược dấu).
    /// </summary>
    public static FinancialAdjustment CreateManual(int ownerProfileId, decimal amount, string? reason, int createdByUserId, int? sanctionId,
        FinancialAdjustment? reverses, DateTime nowUtc)
    {
        if (ownerProfileId <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");
        if (amount == 0) throw new ValidationException("Số tiền điều chỉnh phải khác 0.");
        if (amount != decimal.Truncate(amount)) throw new ValidationException("Số tiền điều chỉnh phải là số nguyên VND.");
        if (Math.Abs(amount) > MaxAbsoluteAmount) throw new ValidationException("Số tiền điều chỉnh vượt giới hạn 10 tỷ VND.");
        if (sanctionId is <= 0) throw new ValidationException("sanctionId phải là số nguyên dương.");
        if (reverses is not null)
        {
            if (reverses.OwnerProfileId != ownerProfileId) throw new ValidationException("Bút toán cần đảo thuộc chủ bãi khác.");
            if (reverses.Status != FinancialAdjustmentStatus.Approved) throw new ConflictException("Chỉ đảo được bút toán đã duyệt.");
            if (amount != -reverses.Amount) throw new ValidationException($"Bút toán đảo phải có số tiền {-reverses.Amount:0}.");
        }
        return new FinancialAdjustment
        {
            OwnerProfileId = ownerProfileId,
            Amount = amount,
            Reason = Settlement.RequireText(reason, "Lý do điều chỉnh", ReasonMaxLength),
            CreatedByUserId = createdByUserId,
            Status = FinancialAdjustmentStatus.PendingApproval,
            SourceType = sanctionId is null ? FinancialAdjustmentSource.Manual : FinancialAdjustmentSource.Sanction,
            SourceReference = sanctionId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ReversesAdjustmentId = reverses?.Id,
            CreatedAtUtc = nowUtc,
        };
    }

    /// <summary>Duyệt: chỉ bút toán chờ duyệt; người tạo không tự duyệt khi bật 4 mắt (403). Được gắn vào kỳ Draft lần sinh / tính lại kế tiếp.</summary>
    public void Approve(int userId, DateTime nowUtc, bool requireDifferentApprover)
    {
        EnsurePending("duyệt");
        if (requireDifferentApprover && CreatedByUserId == userId)
            throw new ForbiddenException($"Người tạo bút toán #{Id} không được tự duyệt – cần một Admin khác.");
        Status = FinancialAdjustmentStatus.Approved;
        ApprovedByUserId = userId;
        ApprovedAtUtc = nowUtc;
    }

    public void Reject(int userId, string? reason, DateTime nowUtc)
    {
        EnsurePending("từ chối");
        RejectReason = Settlement.RequireText(reason, "Lý do từ chối", ReasonMaxLength);
        Status = FinancialAdjustmentStatus.Rejected;
        RejectedByUserId = userId;
        RejectedAtUtc = nowUtc;
    }

    private void EnsurePending(string action)
    {
        if (Status != FinancialAdjustmentStatus.PendingApproval)
            throw new ConflictException($"Không thể {action} bút toán #{Id} đang ở trạng thái {Status}.");
    }


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
