using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Zones;

public sealed record ZoneDto(
    int Id,
    int ParkingLotId,
    string Code,
    string Name,
    bool IsOutdoor,
    bool IsClosed,
    string? ClosedReason,
    int SortOrder);

public sealed record CreateZoneCommand(
    int ParkingLotId,
    string Code,
    string Name,
    bool IsOutdoor = false,
    int SortOrder = 0);

public sealed record UpdateZoneCommand(
    int Id,
    string Name,
    bool IsOutdoor,
    bool IsClosed,
    string? ClosedReason,
    int SortOrder);

public static class ZoneMapper
{
    public static ZoneDto ToDto(this Zone zone) => new(
        zone.Id,
        zone.ParkingLotId,
        zone.Code,
        zone.Name,
        zone.IsOutdoor,
        zone.IsClosed,
        zone.ClosedReason,
        zone.SortOrder);
}
