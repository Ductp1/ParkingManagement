using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Domain.Entities;

/// <summary>[NotificationService] Token Firebase Cloud Messaging của từng thiết bị để gửi push (US-083, US-086).</summary>
public class DeviceToken : BaseEntity
{
    /// <summary>→ UserService (không FK).</summary>
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public string? DeviceName { get; set; }
    public DateTime LastUsedAtUtc { get; set; }
    /// <summary>FCM báo token hết hạn → đánh dấu để không gửi nữa.</summary>
    public bool IsRevoked { get; set; }
}
