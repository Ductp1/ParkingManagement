using ParkingManagement.SharedKernel.Enums;
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

/// <summary>DTO bãi đỗ xe hiển thị trên danh sách của chủ bãi (Owner Portal).</summary>
public sealed record OwnerParkingLotItemDto(
    int Id,
    string Name,
    string Address,
    string City,
    string? District,
    int TotalSlots,
    int AvailableSlots,
    int MaxHeightCm,
    string Status,
    string OpeningHours,
    DateTime CreatedAtUtc);

/// <summary>Lệnh đăng ký bãi đỗ xe mới (US-053).</summary>
public sealed record CreateParkingLotCommand(
    int OwnerProfileId,
    string Name,
    string Address,
    string City,
    string? District,
    double Latitude,
    double Longitude,
    int TotalSlots,
    int MaxHeightCm,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    IntegrationTier IntegrationTier = IntegrationTier.Manual,
    string? Description = null,
    string? HotlinePhone = null);

/// <summary>Lệnh cập nhật thông tin bãi đỗ xe.</summary>
public sealed record UpdateParkingLotCommand(
    int Id,
    int OwnerProfileId,
    string Name,
    string Address,
    string City,
    string? District,
    int MaxHeightCm,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    string? Description = null,
    string? HotlinePhone = null);

// ==================== CÂY PHÂN CẤP BÃI ĐỖ (HỢP ĐỒNG API SỐ 4 GIAO 23/10) ====================

public sealed record ParkingLotHierarchyDto(
    int Id,
    string Name,
    string Address,
    string City,
    string? District,
    string Status,
    int TotalSlots,
    int AvailableSlots,
    int MaxHeightCm,
    string OpeningHours,
    IReadOnlyList<ZoneHierarchyDto> Zones);

public sealed record ZoneHierarchyDto(
    int Id,
    string Code,
    string Name,
    bool IsOutdoor,
    bool IsClosed,
    int SortOrder,
    IReadOnlyList<FloorHierarchyDto> Floors);

public sealed record FloorHierarchyDto(
    int Id,
    string Name,
    int Level,
    int? MaxHeightCm,
    int? MaxWeightKg,
    int GridColumns,
    int GridRows,
    bool IsClosed,
    IReadOnlyList<SlotHierarchyDto> Slots);

public sealed record SlotHierarchyDto(
    int Id,
    string Code,
    string SlotType,
    string MaxVehicleType,
    int GridX,
    int GridY,
    int WidthCells,
    int HeightCells,
    string State,
    bool IsActive);

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

    public static OwnerParkingLotItemDto ToOwnerItemDto(this ParkingLot lot) => new(
        lot.Id,
        lot.Name,
        lot.Address,
        lot.City,
        lot.District,
        lot.TotalSlots,
        lot.AvailableSlots,
        lot.MaxHeightCm,
        lot.Status.ToString(),
        lot.OpenTime == lot.CloseTime ? "24/7" : $"{lot.OpenTime:HH\\:mm} - {lot.CloseTime:HH\\:mm}",
        lot.CreatedAtUtc);

    public static ParkingLotHierarchyDto ToHierarchyDto(this ParkingLot lot) => new(
        lot.Id,
        lot.Name,
        lot.Address,
        lot.City,
        lot.District,
        lot.Status.ToString(),
        lot.TotalSlots,
        lot.AvailableSlots,
        lot.MaxHeightCm,
        lot.OpenTime == lot.CloseTime ? "24/7" : $"{lot.OpenTime:HH\\:mm} - {lot.CloseTime:HH\\:mm}",
        lot.Zones.Select(z => new ZoneHierarchyDto(
            z.Id,
            z.Code,
            z.Name,
            z.IsOutdoor,
            z.IsClosed,
            z.SortOrder,
            z.Floors.Select(f => new FloorHierarchyDto(
                f.Id,
                f.Name,
                f.Level,
                f.MaxHeightCm,
                f.MaxWeightKg,
                f.GridColumns,
                f.GridRows,
                f.IsClosed,
                f.Slots.Select(s => new SlotHierarchyDto(
                    s.Id,
                    s.Code,
                    s.SlotType.ToString(),
                    s.MaxVehicleType.ToString(),
                    s.GridX,
                    s.GridY,
                    s.WidthCells,
                    s.HeightCells,
                    s.State.ToString(),
                    s.IsActive
                )).ToList()
            )).ToList()
        )).ToList());
}
