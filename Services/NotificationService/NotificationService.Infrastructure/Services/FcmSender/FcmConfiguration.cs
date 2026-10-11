namespace NotificationService.Infrastructure.Services.FcmSender;

/// <summary>
/// Configuration cho Firebase Cloud Messaging. Module: TV6 (S1-T602).
/// </summary>
public sealed class FcmConfiguration
{
    /// <summary>Firebase project ID</summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Path to service account JSON file (hoặc base64 encoded)</summary>
    public string ServiceAccountKey { get; set; } = string.Empty;

    /// <summary>Enable mock mode (không gửi thực tế)</summary>
    public bool UseMock { get; set; } = true;

    public int TimeoutMs { get; set; } = 10000;

    /// <summary>Topic name prefix for subscription (e.g., "parking_notifications")</summary>
    public string TopicPrefix { get; set; } = "parking_notifications";
}
