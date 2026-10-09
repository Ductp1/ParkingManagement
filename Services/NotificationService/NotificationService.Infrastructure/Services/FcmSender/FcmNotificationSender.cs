using Microsoft.Extensions.Logging;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Domain.Constants;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Infrastructure.Services.FcmSender;

/// <summary>
/// Firebase Cloud Messaging (FCM) notification sender.
/// Module: TV6 (S1-T602).
/// </summary>
public sealed class FcmNotificationSender : IFcmNotificationSender
{
    private readonly FcmConfiguration _config;
    private readonly ILogger<FcmNotificationSender> _logger;

    public NotificationChannel SupportedChannel => NotificationChannel.Push;

    public FcmNotificationSender(FcmConfiguration config, ILogger<FcmNotificationSender> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            if (_config.UseMock)
            {
                // Mock mode: log và trả về success
                _logger.LogInformation(
                    "🔔 FCM MOCK: userId={UserId}, title={Title}, templateKey={TemplateKey}",
                    userId, title, templateKey
                );
                return (true, null);
            }

            // Real FCM mode - TODO: implement with Firebase Admin SDK
            _logger.LogWarning("⚠️ FCM Real mode not yet implemented - using mock");
            return (true, null);

            // TODO: Implement using Firebase Admin SDK
            // var messaging = FirebaseMessaging.DefaultInstance;
            // var message = new Message()
            // {
            //     Topic = $"{_config.TopicPrefix}_{userId}",
            //     Notification = new Notification() { Title = title, Body = body },
            //     Data = dataJson != null ? JsonSerializer.Deserialize<Dictionary<string, string>>(dataJson) : null
            // };
            // var result = await messaging.SendAsync(message, cancellationToken);
            // _logger.LogInformation("✅ FCM sent, messageId={MessageId}", result);
            // return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ FCM SEND FAILED: userId={UserId}", userId);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Subscribe user to FCM topic (device token based).
    /// Gọi khi user register device token.
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> SubscribeToTopicAsync(
        string deviceToken,
        int userId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (_config.UseMock)
            {
                _logger.LogInformation(
                    "🔔 FCM TOPIC SUBSCRIBE MOCK: userId={UserId}, topic={Topic}",
                    userId, $"{_config.TopicPrefix}_{userId}"
                );
                return (true, null);
            }

            // TODO: Implement real subscription
            _logger.LogWarning("⚠️ FCM Topic subscription not yet implemented");
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ FCM SUBSCRIBE FAILED: userId={UserId}", userId);
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Unsubscribe user from FCM topic.
    /// Gọi khi user logout.
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> UnsubscribeFromTopicAsync(
        string deviceToken,
        int userId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (_config.UseMock)
            {
                _logger.LogInformation(
                    "🔔 FCM TOPIC UNSUBSCRIBE MOCK: userId={UserId}",
                    userId
                );
                return (true, null);
            }

            // TODO: Implement real unsubscription
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ FCM UNSUBSCRIBE FAILED: userId={UserId}", userId);
            return (false, ex.Message);
        }
    }
}
