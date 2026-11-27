using System.Text.Json;
using AdminService.Application.Features.Sanctions;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Contracts;

namespace AdminService.Application.Features.Notifications;

/// <summary>
/// Tài khoản Admin nhận thông báo ("Notifications:AdminUserIds").
/// shortcut: danh sách Admin cố định trong cấu hình, đổi sang API nội bộ của UserService khi có.
/// </summary>
public sealed record AdminAlertSettings(IReadOnlyList<int> AdminUserIds)
{
    public static AdminAlertSettings Default { get; } = new([DemoIds.AdminUser]);
}

/// <summary>
/// Thông báo in-app cho Admin (chuông trên header Admin Portal). Gọi SAU khi đã lưu dữ liệu và không bao giờ ném lỗi:
/// NotificationService lỗi thì chỉ ghi log, nghiệp vụ vẫn thành công. dataJson.adminPath là màn Admin mở khi bấm thông báo.
/// </summary>
public sealed class AdminAlerts(INotificationClient notifications, AdminAlertSettings settings, ILogger<AdminAlerts> logger)
{
    public async Task RaiseAsync(string templateKey, string title, string body, string adminPath)
    {
        var data = JsonSerializer.Serialize(new { adminPath });
        foreach (var userId in settings.AdminUserIds)
        {
            try
            {
                // Không nhận token của request: người gọi hủy request sau khi đã lưu thì thông báo vẫn phải đi.
                await notifications.SendAsync(new OwnerNotification(userId, templateKey, title, body, data), CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không gửi được thông báo {TemplateKey} cho Admin #{UserId}.", templateKey, userId);
            }
        }
    }
}
