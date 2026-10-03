using ParkingManagement.SharedKernel.Domain;

namespace AdminService.Domain.Entities;

/// <summary>
/// [AdminService] Tham số nghiệp vụ chỉnh được không cần sửa code (UC-44). Module: TV8.
/// VD: CHECKIN_GRACE_PERIOD_MINUTES = 15, DEFAULT_COMMISSION_RATE = 0.10.
/// </summary>
public class SystemConfig : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    /// <summary>int | decimal | bool | string | json – để Admin portal hiển thị đúng ô nhập.</summary>
    public string DataType { get; set; } = "string";
    public string? Description { get; set; }
    public int? UpdatedByUserId { get; set; }

}
