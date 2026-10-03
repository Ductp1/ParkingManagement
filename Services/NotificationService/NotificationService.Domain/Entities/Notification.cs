using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Domain.Entities;

/// <summary>
/// [NotificationService] Thông báo gửi tới 1 user qua 1 kênh (EP14). Module: TV6.
/// Kênh InApp được đẩy real-time qua SignalR hub /hubs/notify.
/// </summary>
public class Notification : BaseEntity
{
    public int UserId { get; set; }
    public int? TemplateId { get; set; }
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    public string TemplateKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    /// <summary>Dữ liệu kèm theo để client điều hướng, VD { "bookingId": 12 }.</summary>
    public string? DataJson { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }

    public NotificationTemplate? Template { get; set; }
}
