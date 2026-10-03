using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

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

    public User User { get; set; } = null!;
    public ICollection<StaffAssignment> StaffAssignments { get; set; } = new List<StaffAssignment>();
}
