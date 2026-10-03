using ParkingManagement.SharedKernel.Domain;

namespace AdminService.Domain.Entities;

/// <summary>[AdminService] Bật/tắt tính năng AI, 3D Map (Đặc tả v3: FEATURE_3D_MAP, FEATURE_AI_LPR...).</summary>
public class FeatureFlag : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? Description { get; set; }
    public int? UpdatedByUserId { get; set; }

}
