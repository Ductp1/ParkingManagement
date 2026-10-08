using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.Slots;

public interface IUpdateSlotUseCase
{
    Task<SlotDto> ExecuteAsync(UpdateSlotCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Cập nhật thông tin cấu trúc của ô đỗ.
/// </summary>
public sealed class UpdateSlotUseCase(
    ISlotRepository slotRepository,
    ILogger<UpdateSlotUseCase> logger) : IUpdateSlotUseCase
{
    public async Task<SlotDto> ExecuteAsync(UpdateSlotCommand command, CancellationToken cancellationToken = default)
    {
        var slot = await slotRepository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException("Ô đỗ (Slot)", command.Id);

        var code = command.Code.Trim().ToUpperInvariant();
        if (await slotRepository.ExistsCodeAsync(slot.FloorId, code, excludeId: command.Id, cancellationToken))
            throw new ConflictException($"Mã ô đỗ '{code}' đã tồn tại trên tầng này.");

        if (await slotRepository.IsGridCellOccupiedAsync(slot.FloorId, command.GridX, command.GridY, excludeId: command.Id, cancellationToken))
            throw new ConflictException($"Tọa độ ({command.GridX}, {command.GridY}) đã bị ô đỗ khác chiếm giữ.");

        slot.Code = code;
        slot.SlotType = command.SlotType;
        slot.MaxVehicleType = command.MaxVehicleType;
        slot.GridX = command.GridX;
        slot.GridY = command.GridY;
        slot.WidthCells = command.WidthCells > 0 ? command.WidthCells : 1;
        slot.HeightCells = command.HeightCells > 0 ? command.HeightCells : 1;
        slot.IsActive = command.IsActive;

        await slotRepository.UpdateAsync(slot, cancellationToken);
        await slotRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Đã cập nhật Slot Id={Id}", slot.Id);

        return slot.ToDto();
    }
}
