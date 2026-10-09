using ParkingManagement.SharedKernel.Enums;
using NotificationService.Application.Features;

namespace NotificationService.Application.Features.Notifications;

/// <summary>
/// Use case: Đánh dấu thông báo là đã đọc. Module: TV6 (S2+).
/// </summary>
public interface IMarkNotificationAsReadUseCase
{
    Task ExecuteAsync(int notificationId, int userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Request cho mark as read.
/// </summary>
public sealed record MarkAsReadRequest(int NotificationId, int UserId);

/// <summary>
/// Lấy danh sách thông báo của user.
/// </summary>
public sealed record GetNotificationsRequest(int UserId, bool UnreadOnly = false, int Page = 1, int PageSize = 20);

/// <summary>
/// Use case: Lấy danh sách thông báo. Module: TV6 (S1).
/// </summary>
public interface IGetNotificationsUseCase
{
    Task<(int UnreadCount, List<NotificationDto> Items)> ExecuteAsync(
        GetNotificationsRequest request,
        CancellationToken cancellationToken = default
    );
}
