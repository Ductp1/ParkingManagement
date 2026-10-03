using ParkingManagement.SharedKernel.Domain;

namespace ParkingService.Domain.Entities;

/// <summary>[ParkingService] Ảnh bãi hiển thị cho tài xế ở trang chi tiết (US-018, US-064).</summary>
public class LotPhoto : BaseEntity
{
    public int ParkingLotId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public int SortOrder { get; set; }
    public bool IsCover { get; set; }

    public ParkingLot ParkingLot { get; set; } = null!;
}
