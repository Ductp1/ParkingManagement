using AdminService.Domain.Enums;
using AdminService.Domain.ValueObjects;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Domain.Entities;

/// <summary>
/// [AdminService] Hồ sơ đăng ký đối tác chủ bãi (US-116). Đối tác nộp qua form public (US-053/UI-17, chưa làm),
/// Admin thẩm định → cấp tài khoản qua UserService (kèm tài khoản nhận tiền) → đối tác kích hoạt.
/// Hợp đồng KHÔNG ký ở bước này: mỗi bãi có hợp đồng riêng (<see cref="LotContract"/>) sau khi KYB của bãi được duyệt.
/// Mọi chuyển trạng thái đi qua <see cref="MoveTo"/> – một chỗ kiểm tra duy nhất, sai → 409.
/// </summary>
public class PartnerApplication : BaseEntity
{
    /// <summary>Bảng chuyển trạng thái T2–T10 (T7 resend giữ nguyên trạng thái).</summary>
    private static readonly IReadOnlyDictionary<PartnerApplicationStatus, PartnerApplicationStatus[]> Transitions =
        new Dictionary<PartnerApplicationStatus, PartnerApplicationStatus[]>
        {
            [PartnerApplicationStatus.Submitted] = [PartnerApplicationStatus.UnderReview],
            [PartnerApplicationStatus.UnderReview] =
            [
                PartnerApplicationStatus.NeedsMoreInfo, PartnerApplicationStatus.AccountIssued,
                PartnerApplicationStatus.Rejected, PartnerApplicationStatus.Cancelled,
            ],
            [PartnerApplicationStatus.NeedsMoreInfo] = [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.Cancelled],
            [PartnerApplicationStatus.AccountIssued] = [PartnerApplicationStatus.AccountIssued, PartnerApplicationStatus.Activated],
            [PartnerApplicationStatus.Activated] = [],
            [PartnerApplicationStatus.Rejected] = [],
            [PartnerApplicationStatus.Cancelled] = [],
        };

    /// <summary>Mã hồ sơ hiển thị, duy nhất: PA-2026-0012.</summary>
    public string Code { get; set; } = string.Empty;

    // Người đăng ký
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Position { get; set; }

    // Pháp nhân
    public PartnerBusinessType BusinessType { get; set; } = PartnerBusinessType.Company;
    public string BusinessName { get; set; } = string.Empty;
    public string TaxCode { get; set; } = string.Empty;
    public string BusinessAddress { get; set; } = string.Empty;
    public string LegalRepresentativeName { get; set; } = string.Empty;
    public string? LegalRepresentativePosition { get; set; }
    public string? BusinessLicenseUrl { get; set; }

    // Bãi dự kiến
    public int LotCount { get; set; }
    public string PrimaryLotAddress { get; set; } = string.Empty;
    public int? EstimatedCarSlots { get; set; }
    public int? EstimatedMotorbikeSlots { get; set; }
    public string? OperatingHours { get; set; }

    public DateTime ConsentAcceptedAtUtc { get; set; }

    public PartnerApplicationStatus Status { get; private set; } = PartnerApplicationStatus.Submitted;
    /// <summary>Admin đang thẩm định (→ UserService, không FK).</summary>
    public int? ReviewerAdminId { get; set; }
    public string? InfoRequestNote { get; set; }
    public string? RejectReason { get; set; }
    public string? CancelReason { get; set; }

    // Tài khoản nhận thanh toán mặc định (nhập ở T6, in vào Phụ lục 02 của hợp đồng từng bãi)
    public string? PayoutBankBin { get; set; }
    public string? PayoutBankName { get; set; }
    public string? PayoutBankAccountNumber { get; set; }
    public string? PayoutBankAccountName { get; set; }

    /// <summary>Kết quả T6 từ UserService (→ UserService, không FK).</summary>
    public int? CreatedUserId { get; set; }
    public int? CreatedOwnerProfileId { get; set; }

    /// <summary>Optimistic concurrency (ServiceDefaults đóng dấu mỗi lần lưu): hai Admin thao tác cùng lúc → người sau 409.</summary>
    public byte[] RowVersion { get; set; } = [];

    public bool IsTerminal => Status is PartnerApplicationStatus.Activated or PartnerApplicationStatus.Rejected
        or PartnerApplicationStatus.Cancelled;

    /// <summary>Tài khoản nhận tiền đã nhập ở T6 (null khi chưa cấp tài khoản).</summary>
    public BankAccount? PayoutBank => PayoutBankBin is null || PayoutBankName is null || PayoutBankAccountNumber is null
                                      || PayoutBankAccountName is null
        ? null
        : new BankAccount(PayoutBankBin, PayoutBankName, PayoutBankAccountNumber, PayoutBankAccountName);

    public static bool CanMove(PartnerApplicationStatus from, PartnerApplicationStatus to)
        => Transitions.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Các trạng thái có thể chuyển tới từ trạng thái hiện tại (để giao diện bật/tắt nút).</summary>
    public static IReadOnlyList<PartnerApplicationStatus> NextStatuses(PartnerApplicationStatus from)
        => Transitions.TryGetValue(from, out var targets) ? targets : [];

    /// <summary>Hồ sơ mới từ form public (T1). Dùng cho seed dev và endpoint public sau này.</summary>
    public static PartnerApplication Submit(string code, DateTime consentAcceptedAtUtc) => new()
    {
        Code = code,
        ConsentAcceptedAtUtc = consentAcceptedAtUtc,
        Status = PartnerApplicationStatus.Submitted,
    };

    /// <summary>Chuyển trạng thái duy nhất: kiểm tra bảng T2–T10, sai → ConflictException (409).</summary>
    public void MoveTo(PartnerApplicationStatus target)
    {
        EnsureCanMove(target);
        Status = target;
    }

    // ===================== Thao tác Admin (US-116, T2 / T3 / T6 / T7 / T9 / T10) =====================

    /// <summary>T2: Submitted → UnderReview; người thẩm định = Admin trong token.</summary>
    public void StartReview(int adminUserId)
    {
        EnsureAdmin(adminUserId);
        MoveTo(PartnerApplicationStatus.UnderReview);
        ReviewerAdminId = adminUserId;
    }

    /// <summary>T3: UnderReview → NeedsMoreInfo, ghi chú bắt buộc (chưa sinh edit token – form public P3 chưa có).</summary>
    public void RequestInfo(int adminUserId, string note)
    {
        EnsureAdmin(adminUserId);
        EnsureCanMove(PartnerApplicationStatus.NeedsMoreInfo);
        InfoRequestNote = Required(note, "Nội dung cần bổ sung");
        ReviewerAdminId ??= adminUserId;
        MoveTo(PartnerApplicationStatus.NeedsMoreInfo);
    }

    /// <summary>Kiểm tra trước khi gọi UserService (T6): chỉ cấp tài khoản khi đang thẩm định.</summary>
    public void EnsureCanIssueAccount()
    {
        if (Status != PartnerApplicationStatus.UnderReview)
            throw new ConflictException(
                $"Không thể chuyển hồ sơ đối tác {Code} từ trạng thái {Status} sang {PartnerApplicationStatus.AccountIssued}.");
    }

    /// <summary>T6: UnderReview → AccountIssued sau khi UserService đã tạo tài khoản; lưu tài khoản nhận tiền mặc định.</summary>
    public void IssueAccount(int adminUserId, BankAccount payoutBank, int userId, int ownerProfileId)
    {
        EnsureAdmin(adminUserId);
        EnsureCanIssueAccount();
        if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId), "UserId không hợp lệ.");
        if (ownerProfileId <= 0) throw new ArgumentOutOfRangeException(nameof(ownerProfileId), "OwnerProfileId không hợp lệ.");

        PayoutBankBin = payoutBank.Bin;
        PayoutBankName = payoutBank.BankName;
        PayoutBankAccountNumber = payoutBank.AccountNumber;
        PayoutBankAccountName = payoutBank.AccountName;
        CreatedUserId = userId;
        CreatedOwnerProfileId = ownerProfileId;
        ReviewerAdminId ??= adminUserId;
        MoveTo(PartnerApplicationStatus.AccountIssued);
    }

    /// <summary>T7: chỉ gửi lại link kích hoạt khi đã cấp tài khoản và chưa kích hoạt. Trả OwnerProfileId bên UserService.</summary>
    public int EnsureCanResendActivation()
    {
        if (Status != PartnerApplicationStatus.AccountIssued)
            throw new ConflictException(
                $"Không thể chuyển hồ sơ đối tác {Code} từ trạng thái {Status} sang {PartnerApplicationStatus.AccountIssued}.");
        return CreatedOwnerProfileId ?? throw new ConflictException($"Hồ sơ đối tác {Code} chưa gắn tài khoản chủ bãi.");
    }

    /// <summary>T9: UnderReview → Rejected (kết thúc), lý do bắt buộc.</summary>
    public void Reject(int adminUserId, string reason)
    {
        EnsureAdmin(adminUserId);
        EnsureCanMove(PartnerApplicationStatus.Rejected);
        RejectReason = Required(reason, "Lý do từ chối");
        ReviewerAdminId ??= adminUserId;
        MoveTo(PartnerApplicationStatus.Rejected);
    }

    /// <summary>T10: UnderReview / NeedsMoreInfo → Cancelled khi đối tác rút hồ sơ; không cho hủy sau khi đã cấp tài khoản.</summary>
    public void Cancel(int adminUserId, string reason)
    {
        EnsureAdmin(adminUserId);
        EnsureCanMove(PartnerApplicationStatus.Cancelled);
        CancelReason = Required(reason, "Lý do hủy");
        MoveTo(PartnerApplicationStatus.Cancelled);
    }

    private void EnsureCanMove(PartnerApplicationStatus target)
    {
        if (!CanMove(Status, target))
            throw new ConflictException($"Không thể chuyển hồ sơ đối tác {Code} từ trạng thái {Status} sang {target}.");
    }

    private static void EnsureAdmin(int adminUserId)
    {
        if (adminUserId <= 0) throw new ArgumentOutOfRangeException(nameof(adminUserId), "Admin không hợp lệ.");
    }

    private static string Required(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException($"{field} không được để trống.");
        var text = value.Trim();
        if (text.Length > 1000) throw new ValidationException($"{field} tối đa 1000 ký tự.");
        return text;
    }
}
