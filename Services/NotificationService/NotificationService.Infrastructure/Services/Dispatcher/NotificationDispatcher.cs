using Microsoft.Extensions.Logging;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Domain.Constants;
using NotificationService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using NotificationService.Infrastructure.Persistence.Repositories;

namespace NotificationService.Infrastructure.Services.Dispatcher;

/// <summary>
/// Notification Dispatcher - điều phối gửi notification qua các kênh.
/// Retry tối đa <see cref="NotificationConstants.MaxRetryAttempts"/> lần TỔNG CỘNG (gồm lần gửi đầu)
/// với exponential backoff, chỉ retry lỗi TẠM THỜI (<see cref="ChannelSendResult.IsTransient"/>).
/// Row DB được lưu trước khi gửi và cập nhật sau mỗi lần thử → sống sót qua restart ứng dụng.
/// Module: TV6 (S1-T602).
/// </summary>
public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly Dictionary<NotificationChannel, INotificationChannelSender> _senders;
    private readonly INotificationRepository _repository;
    private readonly ILogger<NotificationDispatcher> _logger;
    private readonly TimeProvider _timeProvider;

    public NotificationDispatcher(
        INotificationRepository repository,
        ILogger<NotificationDispatcher> logger,
        TimeProvider timeProvider,
        IEmailNotificationSender emailSender,
        ISmsNotificationSender smsSender,
        IFcmNotificationSender fcmSender,
        IInAppNotificationSender inAppSender
    )
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

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
        ArgumentNullException.ThrowIfNull(request);
        if (request.UserId <= 0)
            throw new ValidationException("UserId phải > 0");
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
            throw new ValidationException("Title và Body không được trống");

        // Lưu TRƯỚC khi gửi: nếu app chết giữa chừng, hàng vẫn Pending trong DB và retry job xử lý tiếp.
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
        await _repository.AddAsync(notification, cancellationToken);

        if (!_senders.TryGetValue(request.Channel, out var sender))
        {
            notification.Status = NotificationStatus.Failed;
            notification.LastError = $"Channel {request.Channel} không được hỗ trợ";
            await _repository.UpdateAsync(notification, cancellationToken);
            return new SendNotificationResponse(notification.Id, notification.Status, notification.LastError);
        }

        await SendWithRetryAsync(notification, sender, cancellationToken);

        _logger.LogInformation(
            "📤 DISPATCH END: notificationId={Id}, channel={Channel}, status={Status}, attempts={Attempts}",
            notification.Id, notification.Channel, notification.Status, notification.RetryCount
        );

        return new SendNotificationResponse(
            notification.Id,
            notification.Status,
            notification.Status == NotificationStatus.Sent ? "OK" : notification.LastError ?? "Gửi thất bại"
        );
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

        // Lỗi một kênh KHÔNG được làm mất kết quả của các kênh còn lại.
        foreach (var channel in channels.Distinct())
        {
            try
            {
                var request = new SendNotificationRequest(userId, channel, templateKey, title, body, dataJson);
                results[channel] = await SendAsync(request, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ MULTI-CHANNEL: kênh {Channel} lỗi", channel);
                results[channel] = new SendNotificationResponse(0, NotificationStatus.Failed, ex.Message);
            }
        }

        return results;
    }

    public async Task<SendNotificationResponse> RetryAsync(
        int notificationId,
        CancellationToken cancellationToken = default
    )
    {
        var notification = await _repository.GetByIdAsync(notificationId, cancellationToken);
        if (notification == null)
            return new SendNotificationResponse(notificationId, NotificationStatus.Failed, "Notification không tìm thấy");

        // Idempotent: đã Sent thì không gửi lại (tránh duplicate).
        if (notification.Status == NotificationStatus.Sent)
            return new SendNotificationResponse(notificationId, NotificationStatus.Sent, "OK");

        // Đã hết ngân sách thử → chốt Failed, không gửi thêm.
        if (notification.RetryCount >= NotificationConstants.MaxRetryAttempts)
        {
            notification.Status = NotificationStatus.Failed;
            await _repository.UpdateAsync(notification, cancellationToken);
            return new SendNotificationResponse(
                notificationId,
                NotificationStatus.Failed,
                $"Vượt quá {NotificationConstants.MaxRetryAttempts} lần thử gửi"
            );
        }

        if (!_senders.TryGetValue(notification.Channel, out var sender))
        {
            notification.Status = NotificationStatus.Failed;
            notification.LastError = $"Channel {notification.Channel} không được hỗ trợ";
            await _repository.UpdateAsync(notification, cancellationToken);
            return new SendNotificationResponse(notificationId, NotificationStatus.Failed, notification.LastError);
        }

        var result = await AttemptSendAsync(sender, notification, cancellationToken);
        notification.RetryCount++;
        notification.LastError = result.ErrorMessage;

        if (result.Success)
        {
            notification.Status = NotificationStatus.Sent;
            notification.SentAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        }
        else if (!result.IsTransient || notification.RetryCount >= NotificationConstants.MaxRetryAttempts)
        {
            // Lỗi vĩnh viễn, hoặc đã dùng hết lượt thử → chốt Failed (không còn Pending "treo" gây sai monitoring).
            notification.Status = NotificationStatus.Failed;
        }
        // else: còn lượt thử, giữ Pending cho lần retry sau (background job S2+).

        await _repository.UpdateAsync(notification, cancellationToken);

        _logger.LogInformation(
            "🔄 RETRY END: notificationId={Id}, retryCount={RetryCount}, status={Status}, success={Success}",
            notificationId, notification.RetryCount, notification.Status, result.Success
        );

        return new SendNotificationResponse(
            notificationId,
            notification.Status,
            result.Success ? "OK" : result.ErrorMessage ?? "Retry thất bại"
        );
    }

    /// <summary>Gửi có retry: tối đa MaxRetryAttempts lần tổng cộng, exponential backoff giữa các lần.</summary>
    private async Task SendWithRetryAsync(
        Notification notification,
        INotificationChannelSender sender,
        CancellationToken cancellationToken
    )
    {
        ChannelSendResult lastResult = ChannelSendResult.TransientFailure("Chưa thử gửi");

        for (var attempt = 1; attempt <= NotificationConstants.MaxRetryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lastResult = await AttemptSendAsync(sender, notification, cancellationToken);
            notification.RetryCount = attempt;
            notification.LastError = lastResult.ErrorMessage;

            if (lastResult.Success)
            {
                notification.Status = NotificationStatus.Sent;
                notification.SentAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
                await _repository.UpdateAsync(notification, cancellationToken);
                _logger.LogInformation(
                    "✅ DISPATCH SENT: notificationId={Id}, channel={Channel}, attempt={Attempt}",
                    notification.Id, notification.Channel, attempt
                );
                return;
            }

            if (!lastResult.IsTransient)
            {
                // Lỗi vĩnh viễn (sai cấu hình, tham số sai...) → Failed ngay, KHÔNG retry.
                notification.Status = NotificationStatus.Failed;
                await _repository.UpdateAsync(notification, cancellationToken);
                _logger.LogWarning(
                    "⛔ DISPATCH PERMANENT FAILURE: notificationId={Id}, channel={Channel}, error={Error}",
                    notification.Id, notification.Channel, lastResult.ErrorMessage
                );
                return;
            }

            // Lỗi tạm thời: persist tiến độ (hỗ trợ resume sau restart) rồi chờ backoff nếu còn lượt.
            await _repository.UpdateAsync(notification, cancellationToken);
            if (attempt < NotificationConstants.MaxRetryAttempts)
                await DelayBeforeRetryAsync(attempt, cancellationToken);
        }

        // Đã thử MaxRetryAttempts lần, vẫn lỗi tạm thời → chốt Failed.
        notification.Status = NotificationStatus.Failed;
        await _repository.UpdateAsync(notification, cancellationToken);
        _logger.LogError(
            "❌ DISPATCH FAILED AFTER {Attempts} ATTEMPTS: notificationId={Id}, channel={Channel}, error={Error}",
            NotificationConstants.MaxRetryAttempts, notification.Id, notification.Channel, lastResult.ErrorMessage
        );
    }

    /// <summary>
    /// Một lần gọi sender, bọc timeout ExternalServiceTimeoutMs.
    /// Lỗi ngoài sender được coi là TẠM THỜI (retry được); hủy bởi caller được tôn trọng (throw).
    /// </summary>
    private async Task<ChannelSendResult> AttemptSendAsync(
        INotificationChannelSender sender,
        Notification notification,
        CancellationToken cancellationToken
    )
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(NotificationConstants.ExternalServiceTimeoutMs);

        try
        {
            return await sender.SendAsync(notification, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // Caller yêu cầu hủy → không nuốt exception.
        }
        catch (OperationCanceledException)
        {
            return ChannelSendResult.TransientFailure(
                $"Timeout sau {NotificationConstants.ExternalServiceTimeoutMs} ms khi gửi qua {notification.Channel}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sender ném exception không mong đợi: {Channel}", notification.Channel);
            return ChannelSendResult.TransientFailure(ex.Message);
        }
    }

    /// <summary>Exponential backoff: 5s, 7.5s... dùng TimeProvider để unit test advance thời gian.</summary>
    private Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var delaySeconds = NotificationConstants.RetryDelaySeconds
                           * Math.Pow(NotificationConstants.ExponentialBackoffMultiplier, attempt - 1);
        return Task.Delay(TimeSpan.FromSeconds(delaySeconds), _timeProvider, cancellationToken);
    }
}
