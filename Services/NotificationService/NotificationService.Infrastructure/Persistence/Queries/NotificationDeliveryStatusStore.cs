using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Features.Tracking;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Infrastructure.Persistence.Queries;

/// <summary>
/// US-086: đọc trạng thái gửi theo EventId — mọi notification sinh ra từ 1 event khẩn cấp
/// có DedupeKey bắt đầu bằng "evt:{EventId}:" (format đặt trong EmergencyAlertNotifications /
/// BookingConfirmationNotifications). Đọc-only, AsNoTracking.
/// </summary>
public sealed class NotificationDeliveryStatusStore(NotificationDbContext db) : IDeliveryStatusReadStore
{
    public async Task<List<DeliveryStatusRow>> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var prefix = $"evt:{eventId}:";
        return await db.Notifications.AsNoTracking()
            .Where(n => n.DedupeKey != null && n.DedupeKey.StartsWith(prefix))
            .OrderBy(n => n.Id)
            .Select(n => new DeliveryStatusRow(
                n.Id, n.UserId, n.Channel.ToString(), n.TemplateKey, n.Title,
                n.Status.ToString(), n.SentAtUtc, n.RetryCount, n.LastError, n.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}