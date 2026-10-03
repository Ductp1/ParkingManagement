using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace UserService.Domain.Entities;

/// <summary>
/// [UserService] Nhật ký sự kiện bảo mật: đăng nhập sai, nhập sai OTP, khóa tài khoản... (US-002, US-102).
/// Dùng để phát hiện brute-force và làm bằng chứng khi có khiếu nại bị khóa tài khoản.
/// </summary>
public class SecurityEvent : BaseEntity
{
    /// <summary>null khi đăng nhập sai bằng email/SĐT không tồn tại.</summary>
    public int? UserId { get; set; }
    public SecurityEventType EventType { get; set; }
    /// <summary>Email/SĐT đã nhập (để theo dõi dò tài khoản).</summary>
    public string? Identifier { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Detail { get; set; }

    public User? User { get; set; }
}
