using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Slots;

public interface ICreateSlotUseCase
{
    Task<SlotDto> ExecuteAsync(CreateSlotCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Khai báo ô đỗ xe mới trên Tầng (US-058).
/// </summary>
public sealed class CreateSlotUseCase(
    ISlotRepository slotRepository,
    IFloorRepository floorRepository,
    ILogger<CreateSlotUseCase> logger) : ICreateSlotUseCase
{
    public async Task<SlotDto> ExecuteAsync(CreateSlotCommand command, CancellationToken cancellationToken = default)
    {
        if (command.FloorId <= 0)
            throw new ValidationException("FloorId không hợp lệ.");

        if (string.IsNullOrWhiteSpace(command.Code))
            throw new ValidationException("Mã ô đỗ không được để trống.");

        var floor = await floorRepository.GetByIdAsync(command.FloorId, cancellationToken)
            ?? throw new NotFoundException("Tầng (Floor)", command.FloorId);

        var code = command.Code.Trim().ToUpperInvariant();
        if (await slotRepository.ExistsCodeAsync(command.FloorId, code, cancellationToken: cancellationToken))
            throw new ConflictException($"Mã ô đỗ '{code}' đã tồn tại trên tầng này.");

        if (await slotRepository.IsGridCellOccupiedAsync(command.FloorId, command.GridX, command.GridY, cancellationToken: cancellationToken))
            throw new ConflictException($"Tọa độ ({command.GridX}, {command.GridY}) đã có ô đỗ khác chiếm giữ.");

        var slot = new Slot
        {
            FloorId = command.FloorId,
            Code = code,
            SlotType = command.SlotType,
            MaxVehicleType = command.MaxVehicleType,
            GridX = command.GridX,
            GridY = command.GridY,
            WidthCells = command.WidthCells > 0 ? command.WidthCells : 1,
            HeightCells = command.HeightCells > 0 ? command.HeightCells : 1,
            State = SlotState.Available,
            IsActive = true
        };

        await slotRepository.AddAsync(slot, cancellationToken);
        await slotRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Đã tạo Slot {Code} trên Floor Id={FloorId}", slot.Code, slot.FloorId);

        return slot.ToDto();
    }
}
