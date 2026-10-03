using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Domain.Entities;

/// <summary>
/// [NotificationService] Người dùng bật/tắt từng loại thông báo theo kênh (US-087).
/// Thông báo bắt buộc (OTP, đóng bãi khẩn cấp, xác nhận thanh toán) luôn được gửi, bỏ qua cài đặt này.
/// </summary>
public class NotificationPreference : BaseEntity
{
    /// <summary>→ UserService (không FK).</summary>
    public int UserId { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public bool IsEnabled { get; set; } = true;
    /// <summary>Giờ yên lặng (giờ VN): không gửi push/SMS không khẩn cấp trong khoảng này.</summary>
    public TimeOnly? QuietFrom { get; set; }
    public TimeOnly? QuietTo { get; set; }
}
