using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Application.Features.Dispatcher;

/// <summary>
/// Request để gửi thông báo thủ công (internal API). Module: TV6 (S1-T602).
/// </summary>
public sealed record SendNotificationRequest(
    int UserId,
    NotificationChannel Channel,
    string TemplateKey,
    string Title,
    string Body,
    string? DataJson = null,
    int? TemplateId = null
);

/// <summary>
/// Response từ Dispatcher sau khi xử lý.
/// </summary>
public sealed record SendNotificationResponse(
    int NotificationId,
    NotificationStatus Status,
    string Message
);

/// <summary>
/// Interface chính cho Notification Dispatcher - điều phối gửi thông báo qua các kênh.
/// Module: TV6 (S1-T602).
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Gửi 1 thông báo qua kênh chỉ định.
    /// Nếu gửi thất bại, sẽ lưu vào DB với Status=Pending, RetryCount=0 (không retry ngay).
    /// Background job (S2+) sẽ retry.
    /// </summary>
    Task<SendNotificationResponse> SendAsync(SendNotificationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gửi thông báo qua nhiều kênh cùng lúc (broadcast).
    /// Dùng cho emergency notifications (S3+).
    /// </summary>
    Task<Dictionary<NotificationChannel, SendNotificationResponse>> SendMultiChannelAsync(
        int userId,
        string templateKey,
        string title,
        string body,
        NotificationChannel[] channels,
        string? dataJson = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Retry gửi notification đã lưu với Status=Pending.
    /// Gọi bởi background job hoặc manual trigger.
    /// Nếu vượt MaxRetryAttempts, chuyển Status=Failed.
    /// </summary>
    Task<SendNotificationResponse> RetryAsync(int notificationId, CancellationToken cancellationToken = default);
}
