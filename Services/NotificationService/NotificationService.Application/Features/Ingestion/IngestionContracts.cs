using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Application.Features.Ingestion;

/// <summary>
/// US-083→086 foundation: hợp đồng render + cổng gửi. Handler (Infrastructure) deserialize payload
/// event TRỰC TIẾP thành record trong ParkingManagement.SharedKernel.Contracts (dùng chung wire format
/// với outbox — không nhân bản POCO để tránh drift).
/// </summary>

/// <summary>Kết quả render mẫu: tiêu đề + nội dung đã thay placeholder + id mẫu (lưu vào Notification.TemplateId).</summary>
public sealed record RenderedTemplate(string Title, string Body, int? TemplateId);

/// <summary>
/// Render mẫu thông báo i18n: chọn NotificationTemplate theo (Key, Channel, Language) — fallback "vi" khi thiếu bản dịch.
/// Placeholder dạng {BookingCode} được thay bằng giá trị tương ứng trong dictionary.
/// </summary>
public interface ITemplateRenderer
{
    Task<RenderedTemplate> RenderAsync(string templateKey, NotificationChannel channel, string language,
        IReadOnlyDictionary<string, string> placeholders, CancellationToken cancellationToken = default);
}

/// <summary>Cài đặt 1 row/user: ngôn ngữ nhận thông báo (US-083). Chưa có row → "vi".</summary>
public interface IUserNotificationSettingsPort
{
    Task<string> GetLanguageAsync(int userId, CancellationToken cancellationToken = default);
}

/// <summary>Cổng gửi 1 thông báo qua dispatcher có sẵn (giữ retry/backoff/idempotency của TV6).</summary>
public interface ITransactionalNotificationSender
{
    /// <summary>Gửi qua dispatcher; kèm DedupeKey để replay không tạo bản sao.</summary>
    Task SendAsync(int userId, NotificationChannel channel, string templateKey, string title, string body,
        string? dataJson, string? dedupeKey, CancellationToken cancellationToken = default);
}