using ParkingManagement.SharedKernel.Domain;

namespace AdminService.Domain.Entities;

/// <summary>
/// [AdminService] Lịch sử thay đổi tham số hệ thống (US-098 / UC-44). Mỗi dòng = 1 lần Admin đặt giá trị mới
/// có hiệu lực từ EffectiveFromUtc. Chỉ thêm, không sửa giá trị: SystemConfig.Value vẫn là giá trị gốc,
/// giá trị hiệu lực tại một thời điểm được tính từ các dòng này khi đọc (không có job nền).
/// Thay đổi chưa tới ngày hiệu lực có thể bị hủy – dòng vẫn được giữ lại và đóng dấu hủy.
/// </summary>
public class SystemConfigChange : BaseEntity
{
    public int SystemConfigId { get; set; }
    /// <summary>Giá trị mới, đã chuẩn hóa theo SystemConfig.DataType.</summary>
    public string Value { get; set; } = string.Empty;
    /// <summary>Mốc bắt đầu hiệu lực (UTC). Không bao giờ ở quá khứ tại lúc tạo.</summary>
    public DateTime EffectiveFromUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>→ UserService (không FK).</summary>
    public int CreatedByUserId { get; set; }
    /// <summary>null = thay đổi còn giá trị; khác null = đã bị hủy trước khi hiệu lực.</summary>
    public DateTime? CancelledAtUtc { get; set; }
    public int? CancelledByUserId { get; set; }
    public string? CancelReason { get; set; }

    /// <summary>US-098: Admin đặt giá trị mới cho một tham số, hiệu lực từ effectiveFromUtc (bằng hiện tại = hiệu lực ngay).</summary>
    public static SystemConfigChange Schedule(int systemConfigId, string value, DateTime effectiveFromUtc, string reason, int createdByUserId)
        => new()
        {
            SystemConfigId = systemConfigId,
            Value = value,
            EffectiveFromUtc = effectiveFromUtc,
            Reason = reason,
            CreatedByUserId = createdByUserId,
        };

    /// <summary>US-098: Admin hủy thay đổi trước khi nó hiệu lực. Dòng được giữ lại để truy vết, giá trị không bị sửa.</summary>
    public void Cancel(int cancelledByUserId, string reason, DateTime cancelledAtUtc)
    {
        CancelledAtUtc = cancelledAtUtc;
        CancelledByUserId = cancelledByUserId;
        CancelReason = reason;
    }
}
