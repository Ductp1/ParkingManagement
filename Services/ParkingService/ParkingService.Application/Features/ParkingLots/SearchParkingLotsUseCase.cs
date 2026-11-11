using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.SanctionStates;
using ParkingService.Domain.Services;

namespace ParkingService.Application.Features.ParkingLots;

/// <summary>Input tìm bãi (UC-08 / US-014, US-015, US-016): vị trí, bán kính, chiều cao xe, tiện ích, trạng thái và sắp xếp.</summary>
public sealed record SearchParkingLotsQuery(
    double Latitude,
    double Longitude,
    double RadiusKm = 5,
    int? VehicleHeightCm = null,
    int Limit = 20,
    string? Amenities = null,
    bool OpenNowOnly = false,
    bool HasAvailableSlotsOnly = false,
    string? SortBy = "Distance");

public interface ISearchParkingLotsUseCase
{
    Task<IReadOnlyList<ParkingLotSearchItemDto>> ExecuteAsync(SearchParkingLotsQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Tìm kiếm & Lọc bãi đỗ xe (US-014, US-015, US-016).
/// 1) Lọc thô trong SQL bằng khung tọa độ + chiều cao trần (có index).
/// 2) Tính Haversine chính xác bằng LINQ to Objects, bỏ bãi ngoài bán kính.
/// 3) Lọc nâng cao: tiện ích, mở cửa, còn chỗ.
/// 4) Sắp xếp đa tiêu chí: khoảng cách, số chỗ trống, đánh giá.
/// 5) Giới hạn kết quả (Top-N Limit).
/// US-097: bãi đang bị tạm ngưng (chế tài cấp 3) bị loại, bãi bị hạ thứ hạng (cấp 2) xếp sau mọi bãi khác – cả hai TRƯỚC Take(limit).
/// </summary>
public sealed class SearchParkingLotsUseCase(IParkingLotRepository repository, TimeProvider timeProvider,
    ISanctionStateQueries? sanctionStates = null) : ISearchParkingLotsUseCase
{
    private static readonly string[] ValidSortCriteria = ["Distance", "AvailableSlots", "Rating"];

    public async Task<IReadOnlyList<ParkingLotSearchItemDto>> ExecuteAsync(SearchParkingLotsQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Latitude is < -90 or > 90 || query.Longitude is < -180 or > 180)
            throw new ValidationException("Tọa độ GPS không hợp lệ.");
        if (query.RadiusKm is <= 0 or > 50)
            throw new ValidationException("Bán kính tìm kiếm phải trong khoảng (0, 50] km.");
        if (query.VehicleHeightCm is { } h && h is < 50 or > 500)
            throw new ValidationException("Chiều cao xe phải trong khoảng [50, 500] cm.");
        if (query.Limit is <= 0 or > 100)
            throw new ValidationException("Giới hạn kết quả (limit) phải trong khoảng [1, 100].");
        if (!string.IsNullOrWhiteSpace(query.SortBy) &&
            !ValidSortCriteria.Contains(query.SortBy, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException("Tiêu chí sắp xếp không hợp lệ (hỗ trợ: Distance, AvailableSlots, Rating).");
        }

        var box = GeoDistance.BoundingBox(query.Latitude, query.Longitude, query.RadiusKm);
        // Hard Height Clearance: Xe cao + 10 cm phải <= trần bãi
        var minHeight = query.VehicleHeightCm.HasValue ? query.VehicleHeightCm.Value + 10 : 0;

        var candidates = await repository.FindActiveInBoxAsync(box.MinLat, box.MaxLat, box.MinLng, box.MaxLng, minHeight, cancellationToken);
        var nowVn = TimeOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime.AddHours(7));

        var queryable = candidates
            .Select(l => new { Lot = l, Distance = GeoDistance.HaversineKm(query.Latitude, query.Longitude, l.Latitude, l.Longitude) })
            .Where(x => x.Distance <= query.RadiusKm);

        // Lọc bãi đang mở cửa (US-016)
        if (query.OpenNowOnly)
        {
            queryable = queryable.Where(x => x.Lot.IsOpenAt(nowVn));
        }

        // Lọc bãi còn chỗ trống (US-016)
        if (query.HasAvailableSlotsOnly)
        {
            queryable = queryable.Where(x => x.Lot.AvailableSlots > 0);
        }

        // Lọc theo tiện ích (US-016)
        if (!string.IsNullOrWhiteSpace(query.Amenities))
        {
            var required = query.Amenities
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (required.Length > 0)
            {
                queryable = queryable.Where(x => required.All(req =>
                    x.Lot.Amenities.Any(a =>
                        a.Code.Equals(req, StringComparison.OrdinalIgnoreCase) ||
                        a.Name.Contains(req, StringComparison.OrdinalIgnoreCase))));
            }
        }

        // Chế tài SLA (US-097): bỏ bãi tạm ngưng, bãi bị hạ thứ hạng xếp cuối – trước Take để vẫn đủ limit bãi hợp lệ.
        var matches = queryable.ToList();
        var effects = await LoadSanctionEffectsAsync(matches.Select(x => x.Lot).ToList(), cancellationToken);
        var ranked = matches
            .Where(x => !effects.IsListingSuspended(x.Lot.Id, x.Lot.OwnerProfileId))
            .OrderBy(x => effects.IsRankingPenalized(x.Lot.Id, x.Lot.OwnerProfileId));

        // Sắp xếp linh hoạt đa tiêu chí (US-016)
        var sorted = (query.SortBy?.ToLowerInvariant()) switch
        {
            "availableslots" => ranked.ThenByDescending(x => x.Lot.AvailableSlots).ThenBy(x => x.Distance),
            "rating" => ranked.ThenByDescending(x => x.Lot.RatingAverage).ThenByDescending(x => x.Lot.RatingCount).ThenBy(x => x.Distance),
            _ => ranked.ThenBy(x => x.Distance).ThenByDescending(x => x.Lot.AvailableSlots)
        };

        return sorted
            .Take(query.Limit)
            .Select(x => new ParkingLotSearchItemDto(
                x.Lot.Id,
                x.Lot.Name,
                x.Lot.Address,
                x.Lot.Latitude,
                x.Lot.Longitude,
                Math.Round(x.Distance, 2),
                Math.Max(1, (int)Math.Round(x.Distance / 25.0 * 60.0)),
                x.Lot.AvailableSlots,
                x.Lot.TotalSlots,
                x.Lot.MaxHeightCm,
                x.Lot.IsOpenAt(nowVn),
                x.Lot.CoverImageUrl,
                x.Lot.RatingAverage,
                x.Lot.RatingCount,
                x.Lot.Amenities?.Select(a => a.Name).ToList() ?? []))
            .ToList();
    }

    private async Task<LotSanctionEffects> LoadSanctionEffectsAsync(IReadOnlyList<Domain.Entities.ParkingLot> lots,
        CancellationToken cancellationToken)
    {
        if (sanctionStates is null || lots.Count == 0) return LotSanctionEffects.None;
        var states = await sanctionStates.ListInEffectAsync(
            lots.Select(l => l.Id).ToList(), lots.Select(l => l.OwnerProfileId).Distinct().ToList(),
            timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return new LotSanctionEffects(states);
    }
}
