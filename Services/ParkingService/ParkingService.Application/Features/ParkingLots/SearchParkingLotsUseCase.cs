using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Services;

namespace ParkingService.Application.Features.ParkingLots;

/// <summary>Input tìm bãi (UC-08): vị trí tài xế, bán kính, chiều cao xe (tùy chọn).</summary>
public sealed record SearchParkingLotsQuery(double Latitude, double Longitude, double RadiusKm = 5, int? VehicleHeightCm = null);

public interface ISearchParkingLotsUseCase
{
    Task<IReadOnlyList<ParkingLotSearchItemDto>> ExecuteAsync(SearchParkingLotsQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Tìm bãi gần nhất theo GPS.
/// 1) Lọc thô trong SQL bằng khung tọa độ + chiều cao trần (có index).
/// 2) Tính Haversine chính xác bằng LINQ to Objects, bỏ bãi ngoài bán kính.
/// 3) Sắp xếp: gần nhất trước, cùng khoảng cách thì nhiều chỗ trống hơn trước.
/// </summary>
public sealed class SearchParkingLotsUseCase(IParkingLotRepository repository, TimeProvider timeProvider) : ISearchParkingLotsUseCase
{
    public async Task<IReadOnlyList<ParkingLotSearchItemDto>> ExecuteAsync(SearchParkingLotsQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Latitude is < -90 or > 90 || query.Longitude is < -180 or > 180)
            throw new ValidationException("Tọa độ GPS không hợp lệ.");
        if (query.RadiusKm is <= 0 or > 50)
            throw new ValidationException("Bán kính tìm kiếm phải trong khoảng (0, 50] km.");

        var box = GeoDistance.BoundingBox(query.Latitude, query.Longitude, query.RadiusKm);
        // Xe cao + 10 cm phải ≤ trần bãi → trần tối thiểu = chiều cao xe + 10
        var minHeight = query.VehicleHeightCm is { } h ? h + 10 : 0;

        var candidates = await repository.FindActiveInBoxAsync(box.MinLat, box.MaxLat, box.MinLng, box.MaxLng, minHeight, cancellationToken);
        var nowVn = TimeOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime.AddHours(7));

        return candidates
            .Select(l => new { Lot = l, Distance = GeoDistance.HaversineKm(query.Latitude, query.Longitude, l.Latitude, l.Longitude) })
            .Where(x => x.Distance <= query.RadiusKm)
            .OrderBy(x => x.Distance)
            .ThenByDescending(x => x.Lot.AvailableSlots)
            .Select(x => new ParkingLotSearchItemDto(
                x.Lot.Id, x.Lot.Name, x.Lot.Address, Math.Round(x.Distance, 2),
                x.Lot.AvailableSlots, x.Lot.TotalSlots, x.Lot.MaxHeightCm, x.Lot.IsOpenAt(nowVn)))
            .ToList();
    }
}
