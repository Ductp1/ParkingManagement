namespace NotificationService.Infrastructure.Services.SmsSender;

/// <summary>
/// Configuration cho SMS mock/real provider. Module: TV6 (S1-T602).
/// </summary>
public sealed class SmsConfiguration
{
    /// <summary>API provider: "Twilio", "AWS-SNS", "Mock", etc.</summary>
    public string Provider { get; set; } = "Mock";

    /// <summary>API key/access key</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>API secret</summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>From phone number (Twilio) hoặc sender ID (AWS SNS)</summary>
    public string FromNumber { get; set; } = "+1234567890";

    /// <summary>Enable mock mode (log thay vì gửi thực)</summary>
    public bool UseMock { get; set; } = true;

    /// <summary>
    /// Mock: giả lập GẠCH ĐẦU TIÊN thành công (sau đó mới thành công) – dùng để kiểm thử retry.
    /// 0 = luôn thành công; n = fail tạm thời n lần đầu rồi thành công.
    /// </summary>
    public int MockFailFirstAttempts { get; set; } = 0;

    /// <summary>Mock: giả lập LUÔN thất bại (kể cả lỗi vĩnh viễn) để kiểm thử nhánh Failed.</summary>
    public bool MockAlwaysFail { get; set; } = false;

    public int TimeoutMs { get; set; } = 10000;
}
