using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Domain.Entities;
using NotificationService.API.Hubs;

namespace NotificationService.API.Services;

/// <summary>
/// Broadcast notification qua SignalR hub /hubs/notify.
/// Đăng ký DI ở Program.cs (composition root) – nếu thiếu, resolve IInAppNotificationSender sẽ nổ runtime.
/// Dùng SignalR Groups (user-{userId}) thay vì dictionary tĩnh → multiple server instance vẫn đúng,
/// và connection tự rời group khi disconnect (không stale).
/// Module: TV6 (S1-T602).
/// </summary>
public sealed class SignalRInAppNotificationBroadcaster : IInAppNotificationBroadcaster
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SignalRInAppNotificationBroadcaster> _logger;

    public SignalRInAppNotificationBroadcaster(
        IHubContext<NotificationHub> hubContext,
        ILogger<SignalRInAppNotificationBroadcaster> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task BroadcastAsync(int userId, Notification notification, CancellationToken cancellationToken = default)
    {
        var message = new NotificationMessage(
            notification.Id,
            notification.UserId,
            notification.Channel,
            notification.Title,
            notification.Body,
            notification.DataJson,
            notification.CreatedAtUtc
        );

        // await đầy đủ – không fire-and-forget để lỗi broadcast được dispatcher retry.
        await _hubContext.Clients
            .Group(NotificationHub.UserGroup(userId))
            .SendAsync("ReceiveNotification", message, cancellationToken);

        _logger.LogInformation(
            "📡 SIGNALR BROADCAST: userId={UserId}, notificationId={NotificationId}, channel={Channel}",
            userId, notification.Id, notification.Channel);
    }
}
