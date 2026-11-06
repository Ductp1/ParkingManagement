using NotificationService.Domain.Entities;

namespace NotificationService.Application.Features.Preferences;

/// <summary>
/// Port lưu trữ notification preference. Module: TV6.
/// Mỗi dòng = 1 cặp (TemplateKey, Channel) với bật/tắt riêng.
/// </summary>
public interface INotificationPreferenceRepository
{
    Task<IReadOnlyList<NotificationPreference>> GetByUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Thêm mới nếu chưa có (theo unique index UserId+TemplateKey+Channel), ngược lại cập nhật IsEnabled.</summary>
    Task UpsertAsync(NotificationPreference preference, CancellationToken cancellationToken = default);
}

/// <summary>
/// Khóa TemplateKey đặc biệt dùng cho màn "Cài đặt thông báo" dạng tổng hợp (S4).
/// "*" = áp cho mọi template của kênh đó; 3 dòng loại thông báo dùng kênh đại diện InApp
/// vì flag áp cho MỌI kênh (không gắn với 1 kênh cụ thể).
/// </summary>
public static class NotificationPreferenceKeys
{
    public const string AllTemplates = "*";
    public const string Transactional = "Transactional";
    public const string Marketing = "Marketing";
    public const string Urgent = "Urgent";
}
