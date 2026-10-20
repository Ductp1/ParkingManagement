namespace AdminService.Domain.Enums;

/// <summary>
/// [AdminService] Trạng thái hồ sơ đăng ký đối tác chủ bãi (US-116, "Partner Onboarding - State Machine &amp; Flow" mục 2).
/// Chỉ AdminService dùng nên đặt trong AdminService.Domain (không đưa vào SharedKernel).
/// </summary>
public enum PartnerApplicationStatus
{
    Submitted = 0,
    UnderReview = 1,
    NeedsMoreInfo = 2,
    ContractPending = 3,
    AccountIssued = 4,
    /// <summary>Kết thúc: chủ bãi đã kích hoạt tài khoản (T8).</summary>
    Activated = 5,
    /// <summary>Kết thúc: Admin từ chối (T9).</summary>
    Rejected = 6,
    /// <summary>Kết thúc: hủy theo yêu cầu đối tác (T10).</summary>
    Cancelled = 7,
}

/// <summary>[AdminService] Loại hình pháp nhân của đối tác.</summary>
public enum PartnerBusinessType
{
    Company = 1,
    HouseholdBusiness = 2,
}

/// <summary>[AdminService] Trạng thái một phiên bản hợp đồng tự sinh (T5 / T5b / T6).</summary>
public enum ContractDraftStatus
{
    /// <summary>Phiên bản đang gửi cho đối tác ký.</summary>
    Current = 0,
    /// <summary>Đã bị thay bằng phiên bản mới hơn (T5b) hoặc hồ sơ bị từ chối.</summary>
    Superseded = 1,
    /// <summary>Đối tác đã ký, Admin đã cấp tài khoản (T6).</summary>
    Signed = 2,
}

/// <summary>[AdminService] Trạng thái hợp đồng đối tác đã ký.</summary>
public enum OwnerContractStatus
{
    Active = 0,
    Expired = 1,
    Terminated = 2,
}
