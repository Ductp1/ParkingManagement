using AdminService.Domain.Enums;
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
        if (!CanMove(Status, target))
            throw new ConflictException($"Không thể chuyển hồ sơ đối tác {Code} từ trạng thái {Status} sang {target}.");
        Status = target;
    }
}
