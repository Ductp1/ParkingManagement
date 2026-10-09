using ParkingManagement.SharedKernel.Enums;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Slots;

public sealed record SlotDto(
    int Id,
    int FloorId,
    string Code,
    string SlotType,
    string MaxVehicleType,
    int GridX,
    int GridY,
    int WidthCells,
    int HeightCells,
    string State,
    bool IsActive);

public sealed record CreateSlotCommand(
    int FloorId,
    string Code,
    SlotType SlotType = SlotType.Standard,
    VehicleType MaxVehicleType = VehicleType.Suv,
    int GridX = 0,
    int GridY = 0,
    int WidthCells = 1,
    int HeightCells = 1);

public sealed record BatchCreateSlotsCommand(
    int FloorId,
    string Prefix,
    int Count,
    int StartIndex = 1,
    SlotType SlotType = SlotType.Standard,
    VehicleType MaxVehicleType = VehicleType.Suv,
    int StartGridX = 0,
    int StartGridY = 0);

public sealed record UpdateSlotCommand(
    int Id,
    string Code,
    SlotType SlotType,
    VehicleType MaxVehicleType,
    int GridX,
    int GridY,
    int WidthCells,
    int HeightCells,
    bool IsActive);

public static class SlotMapper
{
    public static SlotDto ToDto(this Slot slot) => new(
        slot.Id,
        slot.FloorId,
        slot.Code,
        slot.SlotType.ToString(),
        slot.MaxVehicleType.ToString(),
        slot.GridX,
        slot.GridY,
        slot.WidthCells,
        slot.HeightCells,
        slot.State.ToString(),
        slot.IsActive);
}
