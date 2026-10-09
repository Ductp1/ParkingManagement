using System.Threading;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Infrastructure.Services.SmsSender;

/// <summary>
/// SMS notification sender - mock mode (mặc định) hoặc provider integration (Twilio/AWS-SNS chưa có).
/// Mock mô phỏng được cả thành công và thất bại (xem SmsConfiguration.MockFailFirstAttempts / MockAlwaysFail)
/// để kiểm thử retry của dispatcher. Provider thật chưa tích hợp → lỗi VĨNH VIỄN (không giả success).
/// Module: TV6 (S1-T602).
/// </summary>
public sealed class SmsNotificationSender : ISmsNotificationSender
{
    private readonly SmsConfiguration _config;
    private readonly ILogger<SmsNotificationSender> _logger;
    private int _mockAttemptCounter;

    public NotificationChannel SupportedChannel => NotificationChannel.Sms;

    public SmsNotificationSender(SmsConfiguration config, ILogger<SmsNotificationSender> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<ChannelSendResult> SendAsync(
        Notification notification,
        CancellationToken cancellationToken = default
    )
    {
        if (_config.UseMock)
            return Task.FromResult(SendMock(notification));

        // Provider thật: Twilio/AWS-SNS chưa được tích hợp → báo lỗi vĩnh viễn rõ ràng thay vì "giả success".
        return Task.FromResult(ChannelSendResult.PermanentFailure(
            $"SMS provider '{_config.Provider}' chưa được tích hợp. " +
            "Để chạy mock, đặt SmsConfiguration__UseMock=true. " +
            "Để gửi thật, cần tích hợp SDK Twilio/AWS-SNS trong SmsNotificationSender."));
    }

    /// <summary>
    /// Mock: MockAlwaysFail → luôn transient-fail;
    /// MockFailFirstAttempts=n → n lần đầu transient-fail, các lần sau thành công.
    /// </summary>
    private ChannelSendResult SendMock(Notification notification)
    {
        if (_config.MockAlwaysFail)
        {
            _logger.LogWarning(
                "📱 SMS MOCK FAIL (always): userId={UserId}, title={Title}",
                notification.UserId, notification.Title
            );
            return ChannelSendResult.TransientFailure("SMS mock: MockAlwaysFail=true (giả lập lỗi mạng)");
        }

        var attempt = Interlocked.Increment(ref _mockAttemptCounter);
        if (attempt <= _config.MockFailFirstAttempts)
        {
            _logger.LogWarning(
                "📱 SMS MOCK FAIL (attempt {Attempt}/{FailFirst}): userId={UserId}",
                attempt, _config.MockFailFirstAttempts, notification.UserId
            );
            return ChannelSendResult.TransientFailure(
                $"SMS mock: lỗi tạm thời lần {attempt} (MockFailFirstAttempts={_config.MockFailFirstAttempts})");
        }

        _logger.LogInformation(
            "📱 SMS MOCK SENT: userId={UserId}, title={Title}, body={Body}",
            notification.UserId, notification.Title, notification.Body
        );
        return ChannelSendResult.Ok();
    }
}
