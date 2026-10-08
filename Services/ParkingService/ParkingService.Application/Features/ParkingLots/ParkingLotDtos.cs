using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.ParkingLots;

/// <summary>DTO trả ra ngoài – tách khỏi Entity để không lộ cấu trúc Domain cho client (US-018).</summary>
public sealed record ParkingLotDto(
    int Id,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    int TotalSlots,
    int AvailableSlots,
    double OccupancyRate,
    int MaxHeightCm,
    string OpeningHours,
    bool IsOpenNow,
    string Status,
    string? Description = null,
    string? HotlinePhone = null,
    string? CoverImageUrl = null,
    decimal RatingAverage = 0,
    int RatingCount = 0,
    bool CanBook = true,
    IReadOnlyList<string>? Amenities = null,
    IReadOnlyList<string>? Photos = null);

/// <summary>1 dòng kết quả tìm kiếm bãi gần nhất.</summary>
public sealed record ParkingLotSearchItemDto(
    int Id,
    string Name,
    string Address,
    double DistanceKm,
    int AvailableSlots,
    int TotalSlots,
    int MaxHeightCm,
    bool IsOpenNow);

public static class ParkingLotMapper
{
    public static ParkingLotDto ToDto(this ParkingLot lot, TimeOnly now) => new(
        lot.Id,
        lot.Name,
        lot.Address,
        lot.Latitude,
        lot.Longitude,
        lot.TotalSlots,
        lot.AvailableSlots,
        lot.OccupancyRate,
        lot.MaxHeightCm,
        lot.OpenTime == lot.CloseTime ? "24/7" : $"{lot.OpenTime:HH\\:mm} - {lot.CloseTime:HH\\:mm}",
        lot.IsOpenAt(now),
        lot.Status.ToString(),
        lot.Description,
        lot.HotlinePhone,
        lot.CoverImageUrl,
        lot.RatingAverage,
        lot.RatingCount,
        lot.IsPubliclyVisible,
        lot.Amenities?.Select(a => a.Name).ToList() ?? [],
        lot.Photos?.OrderBy(p => p.SortOrder).Select(p => p.Url).ToList() ?? []);
}
