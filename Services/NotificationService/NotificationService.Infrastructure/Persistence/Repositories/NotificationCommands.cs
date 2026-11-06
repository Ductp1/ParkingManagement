using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Features.Notifications;

namespace NotificationService.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementation của INotificationCommands (EF Core). Module: TV6 (S2).
/// Idempotent: thông báo đã đọc rồi → trả true nhưng không ghi lại.
/// </summary>
public sealed class NotificationCommands(NotificationDbContext dbContext) : INotificationCommands
{
    public async Task<bool> MarkAsReadForUserAsync(int notificationId, int userId, CancellationToken cancellationToken = default)
    {
        var notification = await dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);

        if (notification is null)
            return false;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}
