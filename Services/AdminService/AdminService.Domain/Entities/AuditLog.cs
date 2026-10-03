using ParkingManagement.SharedKernel.Domain;

namespace AdminService.Domain.Entities;

/// <summary>
/// [AdminService] Nhật ký không được sửa/xóa: đổi giá, đổi layout, hoàn tiền, hủy booking,
/// đổi feature flag... (Kiến trúc v3 §4.2). EntityName + EntityId trỏ tới bản ghi bất kỳ nên không đặt khóa ngoại cho cặp này.
/// </summary>
public class AuditLog : BaseEntity
{
    /// <summary>Service phát sinh, VD "BookingService".</summary>
    public string SourceService { get; set; } = string.Empty;
    public int? UserId { get; set; }
    /// <summary>→ UserService (không FK).</summary>
    public int? OwnerProfileId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? Reason { get; set; }
    public string? IpAddress { get; set; }

}
