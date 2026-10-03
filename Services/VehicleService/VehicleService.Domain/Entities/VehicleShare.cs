using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace VehicleService.Domain.Entities;

/// <summary>
/// [VehicleService] Chia sẻ quyền dùng xe cho người thân (US-013, FR-USER-008 – Phase 2).
/// Người được chia sẻ đặt chỗ được cho xe này nhưng không sửa/xóa xe.
/// </summary>
public class VehicleShare : BaseEntity
{
    public int VehicleId { get; set; }
    /// <summary>Chủ xe (UserService, không FK).</summary>
    public int OwnerUserId { get; set; }
    /// <summary>Người được chia sẻ (UserService, không FK).</summary>
    public int SharedWithUserId { get; set; }
    public VehicleShareStatus Status { get; set; } = VehicleShareStatus.Pending;
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    public Vehicle Vehicle { get; set; } = null!;
}
