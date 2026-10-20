namespace AdminService.Domain.Enums;

/// <summary>
/// [AdminService] Trạng thái hồ sơ đăng ký đối tác chủ bãi (US-116, "Partner Onboarding - State Machine &amp; Flow" mục 14).
/// Hợp đồng ký theo từng bãi (LotContract) nên hồ sơ đối tác không còn bước ContractPending.
/// Chỉ AdminService dùng nên đặt trong AdminService.Domain (không đưa vào SharedKernel).
/// </summary>
public enum PartnerApplicationStatus
{
    Submitted = 0,
    UnderReview = 1,
    NeedsMoreInfo = 2,
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

/// <summary>[AdminService] Trạng thái hợp đồng của một bãi đỗ (US-116, mục 14.4).</summary>
public enum LotContractStatus
{
    /// <summary>Đã sinh, chờ ký (có thể sửa sang phiên bản mới).</summary>
    Pending = 0,
    /// <summary>Đã ký, bãi đã được kích hoạt.</summary>
    Signed = 1,
    /// <summary>Hủy trước khi ký.</summary>
    Cancelled = 2,
    /// <summary>Chấm dứt sau khi ký (chưa có endpoint – dự phòng).</summary>
    Terminated = 3,
}

/// <summary>[AdminService] Trạng thái một phiên bản hợp đồng bãi.</summary>
public enum LotContractVersionStatus
{
    /// <summary>Phiên bản đang chờ ký.</summary>
    Current = 0,
    /// <summary>Đã bị thay bằng phiên bản mới hơn hoặc hợp đồng bị hủy.</summary>
    Superseded = 1,
    /// <summary>Phiên bản đã được ký.</summary>
    Signed = 2,
}
