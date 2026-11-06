using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace UserService.Domain.Entities;

/// <summary>
/// [UserService] Tài khoản dùng chung cho cả 4 vai trò (UC-01..06). Module: TV1.
/// </summary>
public class User : BaseEntity, ISoftDelete
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool PhoneConfirmed { get; set; }

    /// <summary>BCrypt hash (cost 12) – không bao giờ lưu mật khẩu gốc.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserStatus Status { get; set; } = UserStatus.PendingVerification;
    public DateTime? LockedUntilUtc { get; set; }
    public string? LockReason { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public string? AvatarUrl { get; set; }

    // ===== KYC (Nghị định 13/2023) =====
    public KycStatus KycStatus { get; set; } = KycStatus.NotSubmitted;
    public KycDocumentType? KycDocumentType { get; set; }
    /// <summary>Số CCCD/GPLX đã mã hóa AES-256.</summary>
    public string? KycNumberEncrypted { get; set; }
    public string? KycFrontImageUrl { get; set; }
    public string? KycBackImageUrl { get; set; }
    public DateTime? KycVerifiedAtUtc { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public ICollection<UserRole> Roles { get; set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<SecurityEvent> SecurityEvents { get; set; } = new List<SecurityEvent>();
    public ICollection<DataSubjectRequest> DataRequests { get; set; } = new List<DataSubjectRequest>();
    public OwnerProfile? OwnerProfile { get; set; }
    public ICollection<StaffAssignment> StaffAssignments { get; set; } = new List<StaffAssignment>();
}
