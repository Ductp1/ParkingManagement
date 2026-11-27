using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace UserService.Domain.Entities;

/// <summary>
/// [UserService] Hồ sơ Chủ bãi (LOT_OWNER). OwnerProfileId chính là TENANT ID:
/// mọi bảng thuộc về bãi đều có OwnerProfileId để cách ly dữ liệu giữa các chủ bãi (SRS v2 F3.6, F6.4).
/// </summary>
public class OwnerProfile : BaseEntity
{
    public int UserId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string? BusinessAddress { get; set; }

    // ===== Tài khoản nhận tiền (VietQR / quyết toán) =====
    public string BankBin { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankAccountName { get; set; } = string.Empty;

    /// <summary>Tỷ lệ hoa hồng riêng theo hợp đồng; null = dùng mặc định hệ thống (10%).</summary>
    public decimal? CommissionRateOverride { get; set; }
    public bool IsLocked { get; set; }

    // ===== Trạng thái hợp tác (US-057: chủ bãi xin rời nền tảng phải báo trước) =====
    public OwnerStatus Status { get; set; } = OwnerStatus.Active;
    public DateTime? ExitRequestedAtUtc { get; set; }
    /// <summary>Ngày chính thức ngừng hợp tác = ngày yêu cầu + thời hạn báo trước (cấu hình OWNER_EXIT_NOTICE_DAYS).</summary>
    public DateTime? ExitEffectiveAtUtc { get; set; }
    /// <summary>Lý do xin rời nền tảng (US-057).</summary>
    public string? ExitReason { get; set; }

    public User User { get; set; } = null!;
    public ICollection<StaffAssignment> StaffAssignments { get; set; } = new List<StaffAssignment>();

    /// <summary>Nộp đơn xin rời nền tảng (US-057): chuyển sang Leaving và tính hạn 30 ngày.</summary>
    public void RequestExit(string reason, int noticeDays, DateTime nowUtc)
    {
        if (Status != OwnerStatus.Active)
            throw new ConflictException($"Chỉ chủ bãi đang hoạt động (Active) mới có thể gửi yêu cầu rời nền tảng (hiện tại: {Status}).");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationException("Lý do xin rời nền tảng không được để trống.");

        if (noticeDays < 1)
            throw new ValidationException("Thời hạn báo trước phải tối thiểu 1 ngày.");

        Status = OwnerStatus.Leaving;
        ExitReason = reason.Trim();
        ExitRequestedAtUtc = nowUtc;
        ExitEffectiveAtUtc = nowUtc.AddDays(noticeDays);
    }

    /// <summary>Rút lại yêu cầu rời nền tảng (US-057): chuyển về Active.</summary>
    public void CancelExit(DateTime nowUtc)
    {
        if (Status != OwnerStatus.Leaving)
            throw new ConflictException($"Không thể hủy yêu cầu khi hồ sơ không ở trạng thái Chờ rời nền tảng (hiện tại: {Status}).");

        if (ExitEffectiveAtUtc.HasValue && nowUtc >= ExitEffectiveAtUtc.Value)
            throw new ConflictException("Đã quá thời hạn hiệu lực rời nền tảng, không thể rút lại yêu cầu.");

        Status = OwnerStatus.Active;
        ExitReason = null;
        ExitRequestedAtUtc = null;
        ExitEffectiveAtUtc = null;
    }

    /// <summary>Chính thức chấm dứt hợp tác (US-057): chuyển sang Terminated, khóa tài khoản và thu hồi nhân viên.</summary>
    public void Terminate(DateTime nowUtc)
    {
        if (Status == OwnerStatus.Terminated)
            return; // Idempotent

        Status = OwnerStatus.Terminated;
        IsLocked = true;

        foreach (var staff in StaffAssignments)
        {
            if (staff.IsActive)
            {
                staff.IsActive = false;
                staff.RevokedAtUtc = nowUtc;
            }
        }
    }
}
