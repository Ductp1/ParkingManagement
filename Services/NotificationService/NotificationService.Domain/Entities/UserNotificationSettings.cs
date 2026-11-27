using ParkingManagement.SharedKernel.Domain;

namespace NotificationService.Domain.Entities;

/// <summary>
/// [NotificationService] Cài đặt chung 1 row/user (US-083): ngôn ngữ nhận thông báo.
/// Không nằm trong NotificationPreference vì bảng đó là matrix (templateKey × channel).
/// </summary>
public class UserNotificationSettings : BaseEntity
{
    /// <summary>→ UserService (không FK). Unique.</summary>
    public int UserId { get; set; }
    /// <summary>Mã ngôn ngữ ISO: "vi" (mặc định) hoặc "en" — chọn mẫu thông báo i18n.</summary>
    public string Language { get; set; } = "vi";
}