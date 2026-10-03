using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace UserService.Domain.Entities;

/// <summary>[UserService] Mã OTP: hiệu lực 5 phút, sai 3 lần khóa 15 phút (Đặc tả v3 §2.1).</summary>
public class OtpCode : BaseEntity
{
    public int? UserId { get; set; }
    /// <summary>SĐT hoặc email nhận mã.</summary>
    public string Destination { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public OtpPurpose Purpose { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }

    public User? User { get; set; }
}
