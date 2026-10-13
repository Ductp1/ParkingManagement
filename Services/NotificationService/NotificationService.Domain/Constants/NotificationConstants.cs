namespace NotificationService.Domain.Constants;

/// <summary>
/// Hằng số cho Notification Dispatcher. Module: TV6.
/// </summary>
public static class NotificationConstants
{
    /// <summary>Số lần thử lại tối đa khi gửi thất bại</summary>
    public const int MaxRetryAttempts = 3;

    /// <summary>Thời gian (giây) chờ giữa các lần retry - dùng exponential backoff</summary>
    public const int RetryDelaySeconds = 5;

    /// <summary>Multiplier cho exponential backoff (delay = RetryDelaySeconds * Math.Pow(multiplier, retryCount))</summary>
    public const double ExponentialBackoffMultiplier = 1.5;

    /// <summary>
    /// Timeout (ms) khi gọi external services (SMTP, SMS API, FCM).
    /// </summary>
    public const int ExternalServiceTimeoutMs = 10000;

    /// <summary>
    /// Kích thước batch khi lấy pending notifications từ DB để xử lý.
    /// </summary>
    public const int NotificationBatchSize = 100;

    /// <summary>
    /// Trong quá trình S2-S4, nếu ≤ 5 chỗ khóa kênh online/instant booking.
    /// </summary>
    public const int CriticalCapacityThreshold = 5;

    /// <summary>
    /// Heartbeat timeout (giây) - nếu không nhận heartbeat trong thời gian này = Stale.
    /// </summary>
    public const int HeartbeatTimeoutSeconds = 180; // 3 phút

    /// <summary>
    /// Số lần heartbeat thất bại trước khi đánh dấu lot là Stale.
    /// </summary>
    public const int StaleHeartbeatFailureThreshold = 3;
}
