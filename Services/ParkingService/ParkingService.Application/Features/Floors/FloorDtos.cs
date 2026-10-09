using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Floors;

public sealed record FloorDto(
    int Id,
    int ZoneId,
    string Name,
    int Level,
    int? MaxHeightCm,
    int? MaxWeightKg,
    int GridColumns,
    int GridRows,
    bool IsClosed);

public sealed record CreateFloorCommand(
    int ZoneId,
    string Name,
    int Level,
    int? MaxHeightCm,
    int? MaxWeightKg,
    int GridColumns,
    int GridRows);

public sealed record UpdateFloorCommand(
    int Id,
    string Name,
    int? MaxHeightCm,
    int? MaxWeightKg,
    int GridColumns,
    int GridRows,
    bool IsClosed);

public static class FloorMapper
{
    public static FloorDto ToDto(this Floor floor) => new(
        floor.Id,
        floor.ZoneId,
        floor.Name,
        floor.Level,
        floor.MaxHeightCm,
        floor.MaxWeightKg,
        floor.GridColumns,
        floor.GridRows,
        floor.IsClosed);
}
