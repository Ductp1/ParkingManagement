using Microsoft.Extensions.Logging;
using NotificationService.Application.Features.Dispatcher;
using NotificationService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Infrastructure.Services.FcmSender;

/// <summary>
/// Firebase Cloud Messaging (FCM) push sender - mock mode (mặc định) hoặc FCM thật.
/// FCM thật cần package FirebaseAdmin (chưa tham chiếu trong solution) và ServiceAccountKey
/// từ env FcmConfiguration__ServiceAccountKey (KHÔNG commit secret). Khi UseMock=false mà thiếu
/// cấu hình → lỗi VĨNH VIỄN kèm hướng dẫn (không retry, không giả success).
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

    public async Task<ChannelSendResult> SendAsync(
        Notification notification,
        CancellationToken cancellationToken = default
    )
    {
        if (_config.UseMock)
        {
            _logger.LogInformation(
                "🔔 FCM MOCK: userId={UserId}, title={Title}, templateKey={TemplateKey}",
                notification.UserId, notification.Title, notification.TemplateKey
            );
            return ChannelSendResult.Ok();
        }

        // UseMock=false: kiểm tra cấu hình trước.
        if (string.IsNullOrWhiteSpace(_config.ProjectId) || string.IsNullOrWhiteSpace(_config.ServiceAccountKey))
        {
            return ChannelSendResult.PermanentFailure(
                "FCM chưa được cấu hình. Cần: 1) FcmConfiguration__ProjectId, " +
                "2) FcmConfiguration__ServiceAccountKey (path file service-account JSON hoặc JSON base64, đặt qua env), " +
                "3) cài package FirebaseAdmin và hoàn tất FcmIntegration (đang để mở – xem FcmNotificationSender). " +
                "Để chạy dev/test, đặt FcmConfiguration__UseMock=true.");
        }

        // FCM thật sẽ cần: FirebaseApp.Create + FirebaseMessaging.DefaultInstance.SendAsync(message)
        // với device tokens lấy từ bảng DeviceTokens (IsRevoked=false). Trả lỗi vĩnh viễn rõ ràng
        // thay vì im lặng "thành công" như bản cũ.
        await Task.Yield();
        return ChannelSendResult.PermanentFailure(
            "FCM send thật chưa được tích hợp trọn vẹn (cần package FirebaseAdmin). " +
            "Cấu hình đã có – hãy bật lại mock (FcmConfiguration__UseMock=true) cho tới khi tích hợp xong SDK.");
    }
}
