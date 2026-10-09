using Microsoft.Extensions.Logging;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;
using NotificationService.Infrastructure.Persistence.Repositories;

namespace NotificationService.Infrastructure.Services.InAppSender;

/// <summary>
/// In-app notification sender - lưu vào DB + push qua SignalR.
/// Module: TV6 (S1-T602).
/// </summary>
public sealed class InAppNotificationSender : IInAppNotificationSender
{
    private readonly INotificationRepository _repository;
    private readonly ILogger<InAppNotificationSender> _logger;
    private readonly IInAppNotificationBroadcaster _broadcaster;

    public NotificationChannel SupportedChannel => NotificationChannel.InApp;

    public InAppNotificationSender(
        INotificationRepository repository,
        ILogger<InAppNotificationSender> logger,
        IInAppNotificationBroadcaster broadcaster
    )
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
    }

    public async Task<(bool Success, string? ErrorMessage)> SendAsync(
        int userId,
        string title,
        string body,
        string? templateKey = null,
        string? dataJson = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Tạo notification object
            var notification = new Notification
            {
                UserId = userId,
                Channel = NotificationChannel.InApp,
                TemplateKey = templateKey ?? string.Empty,
                Title = title,
                Body = body,
                DataJson = dataJson,
                Status = NotificationStatus.Sent,
                RetryCount = 0,
                SentAtUtc = DateTime.UtcNow,
                IsRead = false
            };

            // Lưu vào DB
            await _repository.AddAsync(notification, cancellationToken);

            // Push real-time qua SignalR
            await _broadcaster.BroadcastAsync(userId, notification, cancellationToken);

            _logger.LogInformation(
                "✅ IN-APP SENT: notificationId={NotificationId}, userId={UserId}, title={Title}",
                notification.Id, userId, title
            );

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ IN-APP SEND FAILED: userId={UserId}", userId);
            return (false, ex.Message);
        }
    }
}

/// <summary>
/// Interface để broadcast notification qua SignalR (implement trong API layer).
/// </summary>
public interface IInAppNotificationBroadcaster
{
    Task BroadcastAsync(int userId, Notification notification, CancellationToken cancellationToken = default);
}
