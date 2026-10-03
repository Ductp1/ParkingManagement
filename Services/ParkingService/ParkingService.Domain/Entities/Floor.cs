using ParkingManagement.SharedKernel.Domain;

namespace ParkingService.Domain.Entities;

/// <summary>[ParkingService] Tầng hầm / tầng nổi, có giới hạn chiều cao, tải trọng và kích thước lưới layout.</summary>
public class Floor : BaseEntity
{
    public int ZoneId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Số tầng: âm = hầm (B1 = -1), 0 = mặt đất.</summary>
    public int Level { get; set; }
    public int? MaxHeightCm { get; set; }
    /// <summary>Tải trọng sàn tối đa/xe (kg) – giàn nâng cơ khí tối đa 2.500 kg.</summary>
    public int? MaxWeightKg { get; set; }
    public int GridColumns { get; set; }
    public int GridRows { get; set; }
    public bool IsClosed { get; set; }

    public Zone Zone { get; set; } = null!;
    public ICollection<Slot> Slots { get; set; } = new List<Slot>();
    public ICollection<LayoutVersion> LayoutVersions { get; set; } = new List<LayoutVersion>();
}
