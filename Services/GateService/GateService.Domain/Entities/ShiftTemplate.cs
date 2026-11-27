using ParkingManagement.SharedKernel.Domain;

namespace GateService.Domain.Entities;

/// <summary>[GateService] Mẫu ca của một bãi (giờ Việt Nam); EndTime ≤ StartTime là ca qua đêm.</summary>
public class ShiftTemplate : BaseEntity
{
    public int ParkingLotId { get; set; }
    public int OwnerProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsCashHolder { get; set; } = true;
    public bool IsActive { get; set; } = true;
}
