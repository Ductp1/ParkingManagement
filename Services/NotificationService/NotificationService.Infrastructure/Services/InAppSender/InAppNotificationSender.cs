using Microsoft.Extensions.Logging;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Infrastructure.Services.InAppSender;

/// <summary>
/// In-app notification sender - chỉ push real-time qua SignalR broadcaster.
/// Row DB đã được NotificationDispatcher lưu trước (nguồn sự truth duy nhất) →
/// sender KHÔNG tạo row mới, nhờ vậy retry không bao giờ nhân đôi thông báo in-app.
/// Module: TV6 (S1-T602).
/// </summary>
public sealed class InAppNotificationSender : IInAppNotificationSender
{
    private readonly ILogger<InAppNotificationSender> _logger;
    private readonly IInAppNotificationBroadcaster _broadcaster;

    public NotificationChannel SupportedChannel => NotificationChannel.InApp;

    public InAppNotificationSender(
        ILogger<InAppNotificationSender> logger,
        IInAppNotificationBroadcaster broadcaster
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
    }

    public async Task<ChannelSendResult> SendAsync(
        Notification notification,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await _broadcaster.BroadcastAsync(notification.UserId, notification, cancellationToken);

            _logger.LogInformation(
                "✅ IN-APP BROADCAST: notificationId={NotificationId}, userId={UserId}, title={Title}",
                notification.Id, notification.UserId, notification.Title
            );

            return ChannelSendResult.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Lỗi broadcast SignalR thường tạm thời (mất kết nối hub) → transient để retry.
            _logger.LogError(ex, "❌ IN-APP BROADCAST FAILED: userId={UserId}", notification.UserId);
            return ChannelSendResult.TransientFailure($"In-app broadcast lỗi: {ex.Message}");
        }
    }
}
