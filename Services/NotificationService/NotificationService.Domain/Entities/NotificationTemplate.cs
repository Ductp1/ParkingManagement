using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Domain.Entities;

/// <summary>[NotificationService] Mẫu nội dung, dùng placeholder dạng {BookingCode}, {LotName}.</summary>
public class NotificationTemplate : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    /// <summary>US-083: ngôn ngữ của mẫu – "vi" (mặc định) hoặc "en". Renderer chọn theo user, fallback "vi".</summary>
    public string Language { get; set; } = "vi";
    public string TitleTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
