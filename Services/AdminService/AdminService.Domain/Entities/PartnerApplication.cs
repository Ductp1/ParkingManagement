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
            [PartnerApplicationStatus.Submitted] = [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.Cancelled],
            [PartnerApplicationStatus.UnderReview] =
            [
                PartnerApplicationStatus.NeedsMoreInfo, PartnerApplicationStatus.AccountIssued,
                PartnerApplicationStatus.Rejected, PartnerApplicationStatus.Cancelled,
            ],
            [PartnerApplicationStatus.NeedsMoreInfo] = [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.Cancelled],
            [PartnerApplicationStatus.AccountIssued] =
                [PartnerApplicationStatus.AccountIssued, PartnerApplicationStatus.Activated, PartnerApplicationStatus.Cancelled],
            [PartnerApplicationStatus.Activated] = [],
            [PartnerApplicationStatus.Rejected] = [],
            [PartnerApplicationStatus.Cancelled] = [],
        };

    /// <summary>Mã hồ sơ hiển thị, duy nhất: PA-2026-0012.</summary>
    public string Code { get; set; } = string.Empty;

    // Người đăng ký
    /// <summary>
    /// US-053: tài khoản đã đăng nhập nộp hồ sơ (→ UserService, không FK). Null = hồ sơ seed / tạo trước khi bắt buộc đăng nhập.
    /// Khi duyệt, UserService nâng chính tài khoản này thành chủ bãi.
    /// </summary>
    public int? ApplicantUserId { get; set; }
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
    /// <summary>Admin hủy hồ sơ và thời điểm hủy (→ UserService, không FK).</summary>
    public int? CancelledByAdminId { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }

    /// <summary>
    /// Gỡ tài khoản chủ bãi chưa kích hoạt bên UserService sau khi hủy hồ sơ đã cấp tài khoản: Pending → Synced / Failed (có
    /// <see cref="CleanupError"/> và thử lại được). NotRequired khi hồ sơ chưa từng cấp tài khoản.
    /// </summary>
    public PartnerSyncStatus CleanupSyncStatus { get; private set; } = PartnerSyncStatus.NotRequired;
    public string? CleanupError { get; private set; }
    public DateTime? CleanupSyncedAtUtc { get; private set; }

    // Yêu cầu bổ sung hồ sơ (T3): các mục cần bổ sung, hạn, thời điểm yêu cầu / đối tác gửi bổ sung, kết quả gửi email.
    /// <summary>Danh sách khóa mục cần bổ sung (JSON mảng chuỗi), ví dụ ["taxCode","doc:idCardBack"]. Null = Admin không chỉ định mục nào.</summary>
    public string? InfoRequestItemsJson { get; private set; }
    public DateTime? InfoRequestedAtUtc { get; private set; }
    public DateTime? InfoRequestDueAtUtc { get; private set; }
    public DateTime? InfoProvidedAtUtc { get; private set; }
    public PartnerSyncStatus InfoRequestEmailStatus { get; private set; } = PartnerSyncStatus.NotRequired;
    public string? InfoRequestEmailError { get; private set; }

    // Tài khoản nhận thanh toán mặc định (nhập ở T6, in vào Phụ lục 02 của hợp đồng từng bãi)
    public string? PayoutBankBin { get; set; }
    public string? PayoutBankName { get; set; }
    public string? PayoutBankAccountNumber { get; set; }
    public string? PayoutBankAccountName { get; set; }

    /// <summary>Kết quả T6 từ UserService (→ UserService, không FK).</summary>
    public int? CreatedUserId { get; set; }
    public int? CreatedOwnerProfileId { get; set; }

    /// <summary>
    /// T3 / T4: SHA-256 của link bổ sung hồ sơ (một lần, có hạn) Admin gửi cho đối tác khi hồ sơ NeedsMoreInfo; token gốc không lưu.
    /// Cấp lại link → link cũ hết hiệu lực; đối tác gửi bổ sung → xóa.
    /// </summary>
    public string? EditTokenHash { get; private set; }
    public DateTime? EditTokenExpiresAtUtc { get; private set; }

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

    /// <summary>T3: UnderReview → NeedsMoreInfo, ghi chú bắt buộc; lưu mục cần bổ sung, hạn và link bổ sung (SHA-256) – cùng một lần.</summary>
    public void RequestInfo(int adminUserId, string note, IReadOnlyList<string>? items = null, DateTime? dueAtUtc = null,
        string? editTokenHash = null, DateTime? nowUtc = null)
    {
        EnsureAdmin(adminUserId);
        EnsureCanMove(PartnerApplicationStatus.NeedsMoreInfo);
        var text = Required(note, "Nội dung cần bổ sung");
        var now = nowUtc ?? DateTime.UtcNow;
        if (editTokenHash is not null) EnsureDueInFuture(dueAtUtc, now);   // kiểm tra TRƯỚC khi đổi trạng thái
        InfoRequestNote = text;
        ReviewerAdminId ??= adminUserId;
        MoveTo(PartnerApplicationStatus.NeedsMoreInfo);
        if (editTokenHash is not null) SetInfoRequest(items, dueAtUtc, editTokenHash, now);
    }

    /// <summary>Gửi lại yêu cầu bổ sung: link cũ hết hiệu lực, hạn bổ sung giữ nguyên hoặc gia hạn.</summary>
    public void ReissueInfoRequest(int adminUserId, string editTokenHash, DateTime? newDueAtUtc, DateTime nowUtc)
    {
        EnsureAdmin(adminUserId);
        if (Status != PartnerApplicationStatus.NeedsMoreInfo)
            throw new ConflictException($"Chỉ gửi lại yêu cầu khi hồ sơ {Code} đang chờ bổ sung (hiện tại: {Status}).");
        SetInfoRequest(InfoRequestItems, newDueAtUtc ?? InfoRequestDueAtUtc, editTokenHash, nowUtc);
    }

    private void SetInfoRequest(IReadOnlyList<string>? items, DateTime? dueAtUtc, string tokenHash, DateTime nowUtc)
    {
        EnsureDueInFuture(dueAtUtc, nowUtc);
        InfoRequestItemsJson = items is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(items) : null;
        InfoRequestedAtUtc = nowUtc;
        InfoRequestDueAtUtc = dueAtUtc;
        InfoProvidedAtUtc = null;
        EditTokenHash = tokenHash;
        // Link hết hạn đúng hạn bổ sung (mặc định do người gọi truyền vào = +7 ngày).
        EditTokenExpiresAtUtc = dueAtUtc ?? nowUtc.AddDays(7);
        InfoRequestEmailStatus = PartnerSyncStatus.Pending;
        InfoRequestEmailError = null;
    }

    private static void EnsureDueInFuture(DateTime? dueAtUtc, DateTime nowUtc)
    {
        if (dueAtUtc is { } due && due <= nowUtc) throw new ValidationException("Hạn bổ sung phải ở tương lai.");
    }

    /// <summary>Các khóa mục cần bổ sung Admin đã chọn (rỗng = cho sửa mọi mục).</summary>
    public IReadOnlyList<string> InfoRequestItems => string.IsNullOrEmpty(InfoRequestItemsJson)
        ? []
        : System.Text.Json.JsonSerializer.Deserialize<List<string>>(InfoRequestItemsJson) ?? [];

    public void MarkInfoEmailSent() { InfoRequestEmailStatus = PartnerSyncStatus.Synced; InfoRequestEmailError = null; }

    public void MarkInfoEmailFailed(string error)
    {
        InfoRequestEmailStatus = PartnerSyncStatus.Failed;
        InfoRequestEmailError = error.Length > 300 ? error[..300] : error;
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

    /// <summary>
    /// T10: Submitted / UnderReview / NeedsMoreInfo / AccountIssued (chủ bãi chưa kích hoạt) → Cancelled. Đã Activated → 409.
    /// Thu hồi link bổ sung. Hồ sơ đã cấp tài khoản thì đánh dấu cần gỡ tài khoản bên UserService (CleanupSyncStatus = Pending).
    /// </summary>
    public void Cancel(int adminUserId, string reason, DateTime? nowUtc = null)
    {
        EnsureAdmin(adminUserId);
        if (Status == PartnerApplicationStatus.Activated)
            throw new ConflictException($"Chủ bãi của hồ sơ {Code} đã kích hoạt tài khoản – hãy dùng Đình chỉ hoặc Xóa user thay vì hủy hồ sơ.");
        EnsureCanMove(PartnerApplicationStatus.Cancelled);
        var hadAccount = Status == PartnerApplicationStatus.AccountIssued && CreatedOwnerProfileId is not null;
        CancelReason = Required(reason, "Lý do hủy");
        CancelledByAdminId = adminUserId;
        CancelledAtUtc = nowUtc ?? DateTime.UtcNow;
        EditTokenHash = null;
        EditTokenExpiresAtUtc = null;
        InfoRequestEmailStatus = PartnerSyncStatus.NotRequired;
        CleanupSyncStatus = hadAccount ? PartnerSyncStatus.Pending : PartnerSyncStatus.NotRequired;
        MoveTo(PartnerApplicationStatus.Cancelled);
    }

    /// <summary>Hồ sơ đã hủy và còn việc gỡ tài khoản bên UserService (Pending / Failed) → trả OwnerProfileId cần gỡ.</summary>
    public int EnsureCanRetryCleanup()
    {
        if (Status != PartnerApplicationStatus.Cancelled || CleanupSyncStatus is not (PartnerSyncStatus.Pending or PartnerSyncStatus.Failed))
            throw new ConflictException($"Hồ sơ {Code} không có việc gỡ tài khoản nào cần thử lại.");
        return CreatedOwnerProfileId ?? throw new ConflictException($"Hồ sơ đối tác {Code} chưa gắn tài khoản chủ bãi.");
    }

    public void MarkCleanupSynced(DateTime nowUtc)
    {
        CleanupSyncStatus = PartnerSyncStatus.Synced;
        CleanupError = null;
        CleanupSyncedAtUtc = nowUtc;
    }

    public void MarkCleanupFailed(string error)
    {
        CleanupSyncStatus = PartnerSyncStatus.Failed;
        CleanupError = error.Length > 300 ? error[..300] : error;
    }

    // ===================== Đối tác bổ sung hồ sơ (T3 link / T4) và kích hoạt (T8) =====================

    /// <summary>T3: cấp (hoặc cấp lại) link bổ sung khi hồ sơ đang NeedsMoreInfo; lưu SHA-256 của token.</summary>
    public void IssueEditToken(int adminUserId, string tokenHash, DateTime expiresAtUtc)
    {
        EnsureAdmin(adminUserId);
        if (Status != PartnerApplicationStatus.NeedsMoreInfo)
            throw new ConflictException($"Chỉ cấp link bổ sung khi hồ sơ {Code} đang chờ bổ sung (hiện tại: {Status}).");
        EditTokenHash = string.IsNullOrWhiteSpace(tokenHash) ? throw new ArgumentException("Thiếu token.", nameof(tokenHash)) : tokenHash;
        EditTokenExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>Đúng token của link bổ sung (so sánh thời gian hằng số), chưa xét hạn / trạng thái.</summary>
    public bool MatchesEditToken(string tokenHash)
        => EditTokenHash is not null && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(EditTokenHash), System.Text.Encoding.ASCII.GetBytes(tokenHash));

    /// <summary>Link bổ sung còn dùng được: đúng token, chưa hết hạn, hồ sơ vẫn đang chờ bổ sung.</summary>
    public bool AcceptsEditToken(string tokenHash, DateTime nowUtc)
        => Status == PartnerApplicationStatus.NeedsMoreInfo && EditTokenHash is not null && EditTokenExpiresAtUtc > nowUtc
           && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
               System.Text.Encoding.ASCII.GetBytes(EditTokenHash), System.Text.Encoding.ASCII.GetBytes(tokenHash));

    /// <summary>T4: đối tác gửi lại hồ sơ đã sửa → UnderReview (Admin đang thẩm định tiếp), link hết hiệu lực.</summary>
    public void Resubmit(DateTime consentAcceptedAtUtc)
    {
        EnsureCanMove(PartnerApplicationStatus.UnderReview);
        if (Status != PartnerApplicationStatus.NeedsMoreInfo)
            throw new ConflictException($"Hồ sơ {Code} không ở trạng thái chờ bổ sung.");
        ConsentAcceptedAtUtc = consentAcceptedAtUtc;
        EditTokenHash = null;
        EditTokenExpiresAtUtc = null;
        InfoProvidedAtUtc = consentAcceptedAtUtc;
        MoveTo(PartnerApplicationStatus.UnderReview);
    }

    /// <summary>T8: chủ bãi đã tự đặt mật khẩu bên UserService → hồ sơ hoàn tất.</summary>
    public void MarkActivated()
    {
        if (Status != PartnerApplicationStatus.AccountIssued)
            throw new ConflictException($"Không thể chuyển hồ sơ đối tác {Code} từ trạng thái {Status} sang {PartnerApplicationStatus.Activated}.");
        MoveTo(PartnerApplicationStatus.Activated);
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
