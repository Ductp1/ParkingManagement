using ParkingManagement.SharedKernel.Domain;

namespace ParkingService.Domain.Entities;

/// <summary>
/// [ParkingService] Bãi CHƯA hợp tác do Admin nạp (US-101, UC-60, SRS v2 F4.3 – Phase 2).
/// Hiển thị trong kết quả tìm kiếm với nhãn "Ngoài hệ thống – chỉ dẫn đường", không đặt chỗ được.
/// </summary>
public class ExternalParkingLot : BaseEntity, ISoftDelete
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    /// <summary>Giá tham khảo/giờ – chỉ để hiển thị.</summary>
    public decimal? ReferencePricePerHour { get; set; }
    public string? OpeningHoursText { get; set; }
    public string? Source { get; set; }
    public int ImportedByUserId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}
