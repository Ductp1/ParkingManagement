using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Floors;

public interface ICreateFloorUseCase
{
    Task<FloorDto> ExecuteAsync(CreateFloorCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Tạo Tầng mới trong Khu vực (Zone). Tự động khởi tạo ô đỗ theo lưới.
/// </summary>
public sealed class CreateFloorUseCase(
    IFloorRepository floorRepository,
    IZoneRepository zoneRepository,
    ISlotRepository slotRepository,
    ILogger<CreateFloorUseCase> logger) : ICreateFloorUseCase
{
    public async Task<FloorDto> ExecuteAsync(CreateFloorCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ZoneId <= 0)
            throw new ValidationException("ZoneId không hợp lệ.");

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ValidationException("Tên tầng không được để trống.");

        if (command.GridColumns <= 0 || command.GridRows <= 0)
            throw new ValidationException("Kích thước lưới của tầng (GridColumns, GridRows) phải lớn hơn 0.");

        var zone = await zoneRepository.GetByIdAsync(command.ZoneId, cancellationToken)
            ?? throw new NotFoundException("Khu vực (Zone)", command.ZoneId);

        if (await floorRepository.ExistsNameAsync(command.ZoneId, command.Name.Trim(), cancellationToken: cancellationToken))
            throw new ConflictException($"Tầng mang tên '{command.Name.Trim()}' đã tồn tại trong khu vực này.");

        var floor = new Floor
        {
            ZoneId = command.ZoneId,
            Name = command.Name.Trim(),
            Level = command.Level,
            MaxHeightCm = command.MaxHeightCm,
            MaxWeightKg = command.MaxWeightKg,
            GridColumns = command.GridColumns,
            GridRows = command.GridRows,
            IsClosed = false
        };

        await floorRepository.AddAsync(floor, cancellationToken);
        await floorRepository.SaveChangesAsync(cancellationToken);

        // Tự động sinh danh sách ô đỗ (Slots) ban đầu cho tầng mới (Cách 2)
        var totalCells = command.GridColumns * command.GridRows;
        var defaultCount = Math.Min(totalCells, 20);
        var count = command.InitialSlotCount.HasValue && command.InitialSlotCount.Value > 0
            ? Math.Min(command.InitialSlotCount.Value, totalCells)
            : defaultCount;

        var prefix = command.Level < 0
            ? $"B{Math.Abs(command.Level)}"
            : command.Level == 0
                ? "G"
                : $"F{command.Level}";

        var slots = new List<Slot>();
        for (int i = 0; i < count; i++)
        {
            var gridX = i % command.GridColumns;
            var gridY = i / command.GridColumns;
            if (gridY >= command.GridRows) break;

            slots.Add(new Slot
            {
                FloorId = floor.Id,
                Code = $"{prefix}-{i + 1:D2}",
                SlotType = SlotType.Standard,
                MaxVehicleType = VehicleType.Suv,
                GridX = gridX,
                GridY = gridY,
                WidthCells = 1,
                HeightCells = 1,
                State = SlotState.Available,
                IsActive = true
            });
        }

        if (slots.Count > 0)
        {
            await slotRepository.AddRangeAsync(slots, cancellationToken);
            await slotRepository.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Đã tự động khởi tạo {Count} ô đỗ ({SlotRange}) cho Floor {Name} (Id={FloorId})",
                slots.Count, $"{prefix}-01 .. {prefix}-{slots.Count:D2}", floor.Name, floor.Id);
        }

        logger.LogInformation("Đã tạo Floor {Name} (Level={Level}) cho Zone Id={ZoneId}",
            floor.Name, floor.Level, floor.ZoneId);

        return floor.ToDto();
    }
}
