using ParkingManagement.SharedKernel.Domain;

namespace NotificationService.Domain.Entities;

/// <summary>[NotificationService] Token Firebase Cloud Messaging của từng thiết bị để gửi push (US-083, US-086).</summary>
public class DeviceToken : BaseEntity
{
    /// <summary>→ UserService (không FK).</summary>
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    /// <summary>Platform: "iOS", "Android", "Web", etc.</summary>
    public string Platform { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public DateTime LastUsedAtUtc { get; set; }
    /// <summary>FCM báo token hết hạn → đánh dấu để không gửi nữa.</summary>
    public bool IsRevoked { get; set; }
}
