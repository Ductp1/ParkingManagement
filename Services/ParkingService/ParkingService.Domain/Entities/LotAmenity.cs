using ParkingManagement.SharedKernel.Domain;

namespace ParkingService.Domain.Entities;

/// <summary>
/// [ParkingService] Tiện ích của bãi để tài xế lọc khi tìm kiếm (US-064, US-016):
/// COVERED (mái che), CCTV, SECURITY_24H, EV_CHARGER, CAR_WASH, DISABLED_ACCESS, VALET, TOILET...
/// </summary>
public class LotAmenity : BaseEntity
{
    public int ParkingLotId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Note { get; set; }

    public ParkingLot ParkingLot { get; set; } = null!;
}
