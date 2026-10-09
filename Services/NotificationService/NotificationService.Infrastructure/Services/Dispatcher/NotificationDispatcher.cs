using Microsoft.Extensions.Logging;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Domain.Constants;
using NotificationService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;
using NotificationService.Infrastructure.Persistence.Repositories;

namespace NotificationService.Infrastructure.Services.Dispatcher;

/// <summary>
/// Notification Dispatcher - điều phối gửi notification qua các kênh.
/// Implement retry logic với exponential backoff.
/// Module: TV6 (S1-T602).
/// </summary>
public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly Dictionary<NotificationChannel, INotificationChannelSender> _senders;
    private readonly INotificationRepository _repository;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationRepository repository,
        ILogger<NotificationDispatcher> logger,
        IEmailNotificationSender emailSender,
        ISmsNotificationSender smsSender,
        IFcmNotificationSender fcmSender,
        IInAppNotificationSender inAppSender
    )
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _senders = new Dictionary<NotificationChannel, INotificationChannelSender>
        {
            { NotificationChannel.Email, emailSender ?? throw new ArgumentNullException(nameof(emailSender)) },
            { NotificationChannel.Sms, smsSender ?? throw new ArgumentNullException(nameof(smsSender)) },
            { NotificationChannel.Push, fcmSender ?? throw new ArgumentNullException(nameof(fcmSender)) },
            { NotificationChannel.InApp, inAppSender ?? throw new ArgumentNullException(nameof(inAppSender)) }
        };
    }

    public async Task<SendNotificationResponse> SendAsync(
        SendNotificationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            // Validate
            if (request.UserId <= 0)
                throw new ArgumentException("UserId phải > 0", nameof(request.UserId));

            // Tạo notification entity
            var notification = new Notification
            {
                UserId = request.UserId,
                Channel = request.Channel,
                TemplateId = request.TemplateId,
                TemplateKey = request.TemplateKey ?? string.Empty,
                Title = request.Title,
                Body = request.Body,
                DataJson = request.DataJson,
                Status = NotificationStatus.Pending,
                RetryCount = 0
            };

            // Thử gửi qua channel
            if (!_senders.TryGetValue(request.Channel, out var sender))
            {
                _logger.LogWarning("⚠️ Channel không được support: {Channel}", request.Channel);
                notification.Status = NotificationStatus.Failed;
                notification.LastError = $"Channel {request.Channel} không được hỗ trợ";
                await _repository.AddAsync(notification, cancellationToken);
                return new SendNotificationResponse(
                    notification.Id,
                    notification.Status,
                    notification.LastError
                );
            }

            // Gửi
            var (success, errorMessage) = await sender.SendAsync(
                request.UserId,
                request.Title,
                request.Body,
                request.TemplateKey,
                request.DataJson,
                cancellationToken
            );

            // Cập nhật status dựa trên kết quả
            notification.Status = success ? NotificationStatus.Sent : NotificationStatus.Pending;
            notification.LastError = errorMessage;
            notification.SentAtUtc = success ? DateTime.UtcNow : null;

            // Lưu vào DB
            await _repository.AddAsync(notification, cancellationToken);

            _logger.LogInformation(
                "✅ DISPATCH COMPLETED: notificationId={Id}, channel={Channel}, success={Success}",
                notification.Id, request.Channel, success
            );

            return new SendNotificationResponse(
                notification.Id,
                notification.Status,
                success ? "OK" : errorMessage!
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ DISPATCH ERROR: userId={UserId}, channel={Channel}", request.UserId, request.Channel);

            var errorNotif = new Notification
            {
                UserId = request.UserId,
                Channel = request.Channel,
                TemplateKey = request.TemplateKey ?? string.Empty,
                Title = request.Title,
                Body = request.Body,
                Status = NotificationStatus.Failed,
                LastError = ex.Message
            };
            await _repository.AddAsync(errorNotif, cancellationToken);

            return new SendNotificationResponse(
                errorNotif.Id,
                NotificationStatus.Failed,
                ex.Message
            );
        }
    }

    public async Task<Dictionary<NotificationChannel, SendNotificationResponse>> SendMultiChannelAsync(
        int userId,
        string templateKey,
        string title,
        string body,
        NotificationChannel[] channels,
        string? dataJson = null,
        CancellationToken cancellationToken = default
    )
    {
        var results = new Dictionary<NotificationChannel, SendNotificationResponse>();

        foreach (var channel in channels)
        {
            var request = new SendNotificationRequest(userId, channel, templateKey, title, body, dataJson);
            var response = await SendAsync(request, cancellationToken);
            results[channel] = response;
        }

        return results;
    }

    public async Task<SendNotificationResponse> RetryAsync(
        int notificationId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var notification = await _repository.GetByIdAsync(notificationId, cancellationToken);
            if (notification == null)
                return new SendNotificationResponse(notificationId, NotificationStatus.Failed, "Notification không tìm thấy");

            // Kiểm tra đã vượt quá max retry
            if (notification.RetryCount >= NotificationConstants.MaxRetryAttempts)
            {
                notification.Status = NotificationStatus.Failed;
                await _repository.UpdateAsync(notification, cancellationToken);
                return new SendNotificationResponse(
                    notificationId,
                    NotificationStatus.Failed,
                    $"Vượt quá {NotificationConstants.MaxRetryAttempts} lần retry"
                );
            }

            // Get sender
            if (!_senders.TryGetValue(notification.Channel, out var sender))
            {
                notification.Status = NotificationStatus.Failed;
                notification.LastError = $"Channel {notification.Channel} không được hỗ trợ";
                await _repository.UpdateAsync(notification, cancellationToken);
                return new SendNotificationResponse(notificationId, NotificationStatus.Failed, notification.LastError);
            }

            // Retry send
            var (success, errorMessage) = await sender.SendAsync(
                notification.UserId,
                notification.Title,
                notification.Body,
                notification.TemplateKey,
                notification.DataJson,
                cancellationToken
            );

            // Cập nhật retry count
            notification.RetryCount++;
            notification.Status = success ? NotificationStatus.Sent : NotificationStatus.Pending;
            notification.LastError = errorMessage;
            notification.SentAtUtc = success ? DateTime.UtcNow : notification.SentAtUtc;

            await _repository.UpdateAsync(notification, cancellationToken);

            _logger.LogInformation(
                "🔄 RETRY COMPLETED: notificationId={Id}, retryCount={RetryCount}, success={Success}",
                notificationId, notification.RetryCount, success
            );

            return new SendNotificationResponse(
                notificationId,
                notification.Status,
                success ? "OK" : errorMessage!
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ RETRY ERROR: notificationId={NotificationId}", notificationId);
            return new SendNotificationResponse(notificationId, NotificationStatus.Failed, ex.Message);
        }
    }
}
