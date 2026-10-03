using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace ParkingService.Domain.Entities;

/// <summary>
/// [ParkingService] Ô đỗ cụ thể (VD "B2-C15"). Cấu trúc do TV5 quản lý; trường State do TV6 cập nhật real-time.
/// RowVersion chống 2 request cùng đổi trạng thái 1 slot (NF2).
/// </summary>
public class Slot : BaseEntity
{
    public int FloorId { get; set; }
    public string Code { get; set; } = string.Empty;
    public SlotType SlotType { get; set; } = SlotType.Standard;
    /// <summary>Loại xe lớn nhất được phép đỗ.</summary>
    public VehicleType MaxVehicleType { get; set; } = VehicleType.Suv;
    public int GridX { get; set; }
    public int GridY { get; set; }
    public int WidthCells { get; set; } = 1;
    public int HeightCells { get; set; } = 1;

    public SlotState State { get; set; } = SlotState.Available;
    public DateTime? StateChangedAtUtc { get; set; }

    /// <summary>Dedicated Slot (vé tháng/VIP/EV): gán cố định cho 1 xe.</summary>
    public int? DedicatedVehicleId { get; set; }
    public bool IsActive { get; set; } = true;

    public uint RowVersion { get; set; }   // Postgres: ánh xạ sang cột hệ thống xmin (xem SlotConfiguration)

    public Floor Floor { get; set; } = null!;
    public ICollection<SlotStateLog> StateLogs { get; set; } = new List<SlotStateLog>();
}
