using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Constants;
using NotificationService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementation của INotificationRepository. Module: TV6.
/// </summary>
public sealed class NotificationRepository(NotificationDbContext dbContext) : INotificationRepository
{
    public async Task<Notification?> GetByIdAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);
    }

    public async Task<(int Total, List<Notification> Items)> GetByUserIdAsync(
        int userId,
        bool unreadOnly = false,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default
    )
    {
        var query = dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (total, items);
    }

    public async Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
    }

    public async Task<List<Notification>> GetPendingForRetryAsync(
        int maxRetryAttempts,
        CancellationToken cancellationToken = default
    )

    {
        return await dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.Status == NotificationStatus.Pending &&
                        n.RetryCount < maxRetryAttempts)
            .OrderBy(n => n.CreatedAtUtc)
            .Take(NotificationConstants.NotificationBatchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<Notification> AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        await dbContext.Notifications.AddAsync(notification, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return notification;
    }

    public async Task<Notification> UpdateAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        dbContext.Notifications.Update(notification);
        await dbContext.SaveChangesAsync(cancellationToken);
        return notification;
    }

    public async Task MarkAsReadAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);

        if (notification != null)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);

        if (notification != null)
        {
            dbContext.Notifications.Remove(notification);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
