using Microsoft.EntityFrameworkCore;
using NotificationService.Application.Features;
using ParkingManagement.SharedKernel.Contracts;

namespace NotificationService.Infrastructure.Persistence.Queries;

public sealed class NotificationQueries(NotificationDbContext db) : INotificationQueries
{
    public async Task<PagedResult<NotificationDto>> ListByUserAsync(int userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new NotificationDto(n.Id, n.Channel.ToString(), n.TemplateKey, n.Title, n.Body, n.DataJson,
                n.Status.ToString(), n.IsRead, n.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return new PagedResult<NotificationDto>(items, page, pageSize, total);
    }

    public Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken)
        => db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
}
