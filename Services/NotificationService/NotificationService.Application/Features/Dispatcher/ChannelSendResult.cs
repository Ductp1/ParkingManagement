namespace NotificationService.Application.Features.Dispatcher;

/// <summary>
/// Kết quả gửi 1 lần qua 1 kênh.
/// Phân biệt lỗi TẠM THỜI (retry được, ví dụ timeout, mất mạng) và lỗi VĨNH VIỄN (retry vô ích,
/// ví dụ sai cấu hình, channel không hỗ trợ). Module: TV6 (S1-T602).
/// </summary>
public sealed record ChannelSendResult(bool Success, string? ErrorMessage = null, bool IsTransient = false)
{
    public static ChannelSendResult Ok() => new(true);

    /// <summary>Lỗi tạm thời – dispatcher sẽ thử lại (tối đa NotificationConstants.MaxRetryAttempts lần).</summary>
    public static ChannelSendResult TransientFailure(string errorMessage) => new(false, errorMessage, IsTransient: true);

    /// <summary>Lỗi vĩnh viễn – dispatcher dừng ngay, đánh dấu Failed, không retry.</summary>
    public static ChannelSendResult PermanentFailure(string errorMessage) => new(false, errorMessage, IsTransient: false);
}
