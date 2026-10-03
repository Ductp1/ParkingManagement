using ParkingManagement.SharedKernel.Domain;

namespace UserService.Domain.Entities;

/// <summary>
/// [UserService] Staff (sub-account) gắn với 1 chủ bãi và 1 bãi (staffId → lotOwnerId). UC-32.
/// </summary>
public class StaffAssignment : BaseEntity
{
    public int StaffUserId { get; set; }
    public int OwnerProfileId { get; set; }
    /// <summary>→ ParkingService (không FK).</summary>
    public int ParkingLotId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? RevokedAtUtc { get; set; }

    public User StaffUser { get; set; } = null!;
    public OwnerProfile OwnerProfile { get; set; } = null!;
}
