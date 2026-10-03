using ParkingManagement.SharedKernel.Domain;

namespace ParkingService.Domain.Entities;

/// <summary>[ParkingService] Khu vực / Tòa trong bãi (cấp 2 của cây Facility → Zone → Floor → Slot). Module: TV5.</summary>
public class Zone : BaseEntity
{
    public int ParkingLotId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsOutdoor { get; set; }
    public bool IsClosed { get; set; }
    public string? ClosedReason { get; set; }
    public int SortOrder { get; set; }

    public ParkingLot ParkingLot { get; set; } = null!;
    public ICollection<Floor> Floors { get; set; } = new List<Floor>();
}
