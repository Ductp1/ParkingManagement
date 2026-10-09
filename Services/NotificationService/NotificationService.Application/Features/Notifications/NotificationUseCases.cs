namespace NotificationService.Application.Features.Notifications;

/// <summary>
/// Use case: Đánh dấu thông báo là đã đọc. Module: TV6 (S2).
/// </summary>
public sealed class MarkNotificationAsReadUseCase : IMarkNotificationAsReadUseCase
{
    public async Task ExecuteAsync(int notificationId, int userId, CancellationToken cancellationToken = default)
    {
        if (notificationId <= 0)
            throw new ArgumentException("notificationId phải > 0", nameof(notificationId));

        if (userId <= 0)
            throw new ArgumentException("userId phải > 0", nameof(userId));

        // TODO: Implement when repository is available
        await Task.CompletedTask;
    }
}

/// <summary>
/// Use case: Lấy danh sách thông báo. Module: TV6.
/// </summary>
public sealed class GetNotificationsUseCase(INotificationQueries queries) : IGetNotificationsUseCase
{
    public async Task<(int UnreadCount, List<NotificationDto> Items)> ExecuteAsync(
        GetNotificationsRequest request,
        CancellationToken cancellationToken = default
    )
    {
        if (request.UserId <= 0)
            throw new ArgumentException("UserId phải > 0", nameof(request.UserId));

        if (request.Page < 1 || request.PageSize < 1 || request.PageSize > 100)
            throw new ArgumentException("Page >= 1 và PageSize trong khoảng 1-100");

        var unreadCount = await queries.CountUnreadAsync(request.UserId, cancellationToken);

        var pagedResult = await queries.ListByUserAsync(
            request.UserId,
            request.UnreadOnly,
            request.Page,
            request.PageSize,
            cancellationToken
        ); 

        return (unreadCount, pagedResult.Items.ToList());
    }
}
