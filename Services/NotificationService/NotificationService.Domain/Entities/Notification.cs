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
    /// <summary>
    /// US-083: chặn gửi trùng khi webhook replay/outbox redelivery — key dạng "evt:{EventId}:{Channel}".
    /// Unique filtered index (NOT NULL) ở database; trùng → bỏ qua im lặng (idempotent).
    /// </summary>
    public string? DedupeKey { get; set; }

    /// <summary>
    /// US-086: nhóm các thông báo thuộc cùng 1 đợt khẩn cấp — key dạng "evt:{EventId}".
    /// API tổng hợp trạng thái gửi (delivery-status) query theo key này; null cho thông báo thường.
    /// </summary>
    public string? BatchKey { get; set; }

    public NotificationTemplate? Template { get; set; }
}
