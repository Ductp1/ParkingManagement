using AdminService.Domain.Enums;
using AdminService.Domain.ValueObjects;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Domain.Entities;

/// <summary>
/// [AdminService] Hồ sơ đăng ký đối tác chủ bãi (US-116). Đối tác nộp qua form public (US-053/UI-17, chưa làm),
/// Admin thẩm định → hợp đồng tự sinh → ký offline → Admin cấp tài khoản qua UserService → đối tác kích hoạt.
/// Mọi chuyển trạng thái đi qua <see cref="MoveTo"/> – một chỗ kiểm tra duy nhất, sai → 409.
/// </summary>
public class PartnerApplication : BaseEntity
{
    /// <summary>Bảng chuyển trạng thái T2–T10 (+ T5b revise, T7 resend giữ nguyên trạng thái).</summary>
    private static readonly IReadOnlyDictionary<PartnerApplicationStatus, PartnerApplicationStatus[]> Transitions =
        new Dictionary<PartnerApplicationStatus, PartnerApplicationStatus[]>
        {
            [PartnerApplicationStatus.Submitted] = [PartnerApplicationStatus.UnderReview],
            [PartnerApplicationStatus.UnderReview] =
            [
                PartnerApplicationStatus.NeedsMoreInfo, PartnerApplicationStatus.ContractPending,
                PartnerApplicationStatus.Rejected, PartnerApplicationStatus.Cancelled,
            ],
            [PartnerApplicationStatus.NeedsMoreInfo] = [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.Cancelled],
            [PartnerApplicationStatus.ContractPending] =
            [
                PartnerApplicationStatus.ContractPending, PartnerApplicationStatus.AccountIssued,
                PartnerApplicationStatus.Rejected, PartnerApplicationStatus.Cancelled,
            ],
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
    /// <summary>Số hợp đồng cấp ở T5, giữ nguyên qua mọi phiên bản (HD-SP-2026-052).</summary>
    public string? ContractNumber { get; set; }
    /// <summary>Kết quả T6 từ UserService (→ UserService, không FK).</summary>
    public int? CreatedUserId { get; set; }
    public int? CreatedOwnerProfileId { get; set; }

    /// <summary>Optimistic concurrency (ServiceDefaults đóng dấu mỗi lần lưu): hai Admin thao tác cùng lúc → người sau 409.</summary>
    public byte[] RowVersion { get; set; } = [];

    public ICollection<ContractDraft> ContractDrafts { get; set; } = new List<ContractDraft>();

    public bool IsTerminal => Status is PartnerApplicationStatus.Activated or PartnerApplicationStatus.Rejected
        or PartnerApplicationStatus.Cancelled;

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

    // ===================== Thao tác Admin (US-116, T2 / T3 / T9 / T10) =====================

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

    /// <summary>T9: UnderReview / ContractPending → Rejected (kết thúc), lý do bắt buộc; hợp đồng đang gửi → Superseded.</summary>
    public void Reject(int adminUserId, string reason)
    {
        EnsureAdmin(adminUserId);
        EnsureCanMove(PartnerApplicationStatus.Rejected);
        RejectReason = Required(reason, "Lý do từ chối");
        ReviewerAdminId ??= adminUserId;
        foreach (var draft in ContractDrafts.Where(d => d.Status == ContractDraftStatus.Current))
            draft.Status = ContractDraftStatus.Superseded;
        MoveTo(PartnerApplicationStatus.Rejected);
    }

    /// <summary>T10: UnderReview / ContractPending / NeedsMoreInfo → Cancelled khi đối tác rút hồ sơ; không cho hủy sau khi đã cấp tài khoản.</summary>
    public void Cancel(int adminUserId, string reason)
    {
        EnsureAdmin(adminUserId);
        EnsureCanMove(PartnerApplicationStatus.Cancelled);
        CancelReason = Required(reason, "Lý do hủy");
        foreach (var draft in ContractDrafts.Where(d => d.Status == ContractDraftStatus.Current))
            draft.Status = ContractDraftStatus.Superseded;
        MoveTo(PartnerApplicationStatus.Cancelled);
    }

    // ===================== Hợp đồng tự sinh + cấp tài khoản (US-116, T5 / T5b / T6 / T7) =====================

    /// <summary>Phiên bản hợp đồng đang gửi đối tác ký (tối đa một).</summary>
    public ContractDraft? CurrentDraft => ContractDrafts.FirstOrDefault(d => d.Status == ContractDraftStatus.Current);

    /// <summary>Số phiên bản của lần sinh hợp đồng kế tiếp (v1, v2, …).</summary>
    public int NextDraftVersion => ContractDrafts.Count == 0 ? 1 : ContractDrafts.Max(d => d.Version) + 1;

    /// <summary>Số hợp đồng: giữ số đã cấp ở T5; chưa có thì cấp theo Id hồ sơ (HD-SP-2026-0012) – duy nhất vì Id duy nhất.</summary>
    public string ContractNumberFor(int year) => ContractNumber ?? $"HD-SP-{year}-{Id:0000}";

    /// <summary>Xem trước hợp đồng (A5): chỉ khi đang thẩm định (sắp gửi) hoặc đang chờ ký (sắp sửa). Có kiểm tra người đại diện.</summary>
    public void EnsureCanGenerateContract()
    {
        if (Status is not (PartnerApplicationStatus.UnderReview or PartnerApplicationStatus.ContractPending))
            throw new ConflictException(
                $"Chỉ sinh hợp đồng khi hồ sơ đối tác {Code} đang ở trạng thái UnderReview hoặc ContractPending (hiện tại: {Status}).");
        EnsureLegalRepresentative();
    }

    /// <summary>T5 chỉ từ UnderReview (đang ContractPending thì dùng sửa hợp đồng T5b).</summary>
    public void EnsureCanSendContract()
    {
        EnsureStatus(PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.ContractPending);
        EnsureLegalRepresentative();
    }

    /// <summary>T5b chỉ khi đang ContractPending.</summary>
    public void EnsureCanReviseContract()
    {
        EnsureStatus(PartnerApplicationStatus.ContractPending, PartnerApplicationStatus.ContractPending);
        EnsureLegalRepresentative();
    }

    /// <summary>T5: UnderReview → ContractPending. Cấp số hợp đồng, tạo phiên bản v1 (Current) từ file đã sinh.</summary>
    public ContractDraft SendContract(int adminUserId, ContractTerms terms, int templateVersion, string contractNumber, StoredFile file,
        DateTime nowUtc)
    {
        EnsureAdmin(adminUserId);
        EnsureCanSendContract();
        var draft = AddDraft(adminUserId, terms, templateVersion, contractNumber, file, revisionReason: null, nowUtc);
        ReviewerAdminId ??= adminUserId;
        MoveTo(PartnerApplicationStatus.ContractPending);
        return draft;
    }

    /// <summary>T5b: ContractPending → ContractPending. Phiên bản v+1 (cùng số hợp đồng), bản đang gửi → Superseded; lý do bắt buộc.</summary>
    public ContractDraft ReviseContract(int adminUserId, ContractTerms terms, int templateVersion, StoredFile file, string reason,
        DateTime nowUtc)
    {
        EnsureAdmin(adminUserId);
        EnsureCanReviseContract();
        var text = Required(reason, "Lý do sửa hợp đồng");
        var draft = AddDraft(adminUserId, terms, templateVersion, ContractNumberFor(nowUtc.Year), file, text, nowUtc);
        MoveTo(PartnerApplicationStatus.ContractPending);
        return draft;
    }

    /// <summary>
    /// Kiểm tra trước khi gọi UserService (T6): đang ContractPending, có phiên bản Current và Admin xác nhận đúng phiên bản đó.
    /// Trả phiên bản sẽ được ký.
    /// </summary>
    public ContractDraft EnsureCanIssueAccount(int? confirmedDraftVersion)
    {
        EnsureStatus(PartnerApplicationStatus.ContractPending, PartnerApplicationStatus.AccountIssued);
        var draft = CurrentDraft ?? throw new ConflictException(
            $"Hồ sơ đối tác {Code} chưa có phiên bản hợp đồng đang hiệu lực, hãy gửi lại hợp đồng trước khi cấp tài khoản.");
        if (confirmedDraftVersion is not { } version || version <= 0)
            throw new ValidationException("Vui lòng xác nhận phiên bản hợp đồng đã ký (confirmedDraftVersion).");
        if (version != draft.Version)
            throw new ConflictException(
                $"Phiên bản hợp đồng hiện tại là v{draft.Version}, không phải v{version}. Vui lòng kiểm tra lại bản đã ký.");
        return draft;
    }

    /// <summary>
    /// T6: ContractPending → AccountIssued sau khi UserService đã tạo tài khoản. Phiên bản Current → Signed;
    /// trả hợp đồng đã ký (sao chép điều khoản từ phiên bản đó) để lưu cùng lần SaveChanges.
    /// </summary>
    public OwnerContract IssueAccount(int adminUserId, int confirmedDraftVersion, int userId, int ownerProfileId, StoredFile signedScan)
    {
        EnsureAdmin(adminUserId);
        var draft = EnsureCanIssueAccount(confirmedDraftVersion);
        if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId), "UserId không hợp lệ.");
        if (ownerProfileId <= 0) throw new ArgumentOutOfRangeException(nameof(ownerProfileId), "OwnerProfileId không hợp lệ.");

        draft.Status = ContractDraftStatus.Signed;
        CreatedUserId = userId;
        CreatedOwnerProfileId = ownerProfileId;
        MoveTo(PartnerApplicationStatus.AccountIssued);
        return new OwnerContract
        {
            OwnerProfileId = ownerProfileId,
            PartnerApplicationId = Id,
            PartnerApplication = this,
            ContractDraftId = draft.Id,
            ContractDraft = draft,
            ContractNumber = draft.ContractNumber,
            DraftVersion = draft.Version,
            SignedFileKey = signedScan.Key,
            SignedFileContentType = signedScan.ContentType,
            SignedFileSha256 = signedScan.Sha256,
            CommissionRate = draft.CommissionRate,
            EffectiveFrom = draft.EffectiveFrom,
            EffectiveTo = draft.EffectiveTo,
            Status = OwnerContractStatus.Active,
            CreatedByAdminId = adminUserId,
        };
    }

    /// <summary>T7: chỉ gửi lại link kích hoạt khi đã cấp tài khoản và chưa kích hoạt. Trả OwnerProfileId bên UserService.</summary>
    public int EnsureCanResendActivation()
    {
        EnsureStatus(PartnerApplicationStatus.AccountIssued, PartnerApplicationStatus.AccountIssued);
        return CreatedOwnerProfileId ?? throw new ConflictException($"Hồ sơ đối tác {Code} chưa gắn tài khoản chủ bãi.");
    }

    private ContractDraft AddDraft(int adminUserId, ContractTerms terms, int templateVersion, string contractNumber, StoredFile file,
        string? revisionReason, DateTime nowUtc)
    {
        var version = NextDraftVersion;
        foreach (var current in ContractDrafts.Where(d => d.Status == ContractDraftStatus.Current))
            current.Status = ContractDraftStatus.Superseded;

        var draft = new ContractDraft
        {
            PartnerApplicationId = Id,
            PartnerApplication = this,
            ContractNumber = contractNumber,
            Version = version,
            TemplateVersion = templateVersion,
            CommissionRate = terms.CommissionRate,
            EffectiveFrom = terms.EffectiveFrom,
            EffectiveTo = terms.EffectiveTo,
            TermMonths = terms.TermMonths,
            BankBin = terms.BankBin,
            BankName = terms.BankName,
            BankAccountNumber = terms.BankAccountNumber,
            BankAccountName = terms.BankAccountName,
            FileKey = file.Key,
            FileContentType = file.ContentType,
            FileSha256 = file.Sha256,
            Status = ContractDraftStatus.Current,
            RevisionReason = revisionReason,
            SentAtUtc = nowUtc,
            SentToEmail = Email,
            CreatedByAdminId = adminUserId,
        };
        ContractDrafts.Add(draft);
        ContractNumber = contractNumber;
        return draft;
    }

    /// <summary>Thao tác chỉ hợp lệ ở đúng một trạng thái (bảng chuyển cho phép tự chuyển ContractPending / AccountIssued nên cần kiểm tra riêng).</summary>
    private void EnsureStatus(PartnerApplicationStatus expected, PartnerApplicationStatus target)
    {
        if (Status != expected || !CanMove(Status, target))
            throw new ConflictException($"Không thể chuyển hồ sơ đối tác {Code} từ trạng thái {Status} sang {target}.");
    }

    private void EnsureLegalRepresentative()
    {
        if (string.IsNullOrWhiteSpace(LegalRepresentativeName))
            throw new ConflictException($"Hồ sơ đối tác {Code} chưa có người đại diện pháp luật, chưa thể sinh hợp đồng.");
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
