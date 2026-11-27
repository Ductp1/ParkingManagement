using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Slots;

public interface IBatchCreateSlotsUseCase
{
    Task<int> ExecuteAsync(BatchCreateSlotsCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Tạo hàng loạt ô đỗ xe theo dãy số cho tầng mới (VD A-01 đến A-20).
/// </summary>
public sealed class BatchCreateSlotsUseCase(
    ISlotRepository slotRepository,
    IFloorRepository floorRepository,
    ILogger<BatchCreateSlotsUseCase> logger) : IBatchCreateSlotsUseCase
{
    public async Task<int> ExecuteAsync(BatchCreateSlotsCommand command, CancellationToken cancellationToken = default)
    {
        if (command.FloorId <= 0)
            throw new ValidationException("FloorId không hợp lệ.");

        if (command.Count <= 0 || command.Count > 200)
            throw new ValidationException("Số lượng slot tạo một lần phải từ 1 đến 200.");

        var floor = await floorRepository.GetByIdAsync(command.FloorId, cancellationToken)
            ?? throw new NotFoundException("Tầng (Floor)", command.FloorId);

        var prefix = string.IsNullOrWhiteSpace(command.Prefix) ? "S" : command.Prefix.Trim().ToUpperInvariant();
        var slots = new List<Slot>();

        for (int i = 0; i < command.Count; i++)
        {
            var index = command.StartIndex + i;
            var code = $"{prefix}-{index:D2}";

            // Kiểm tra trùng mã
            if (await slotRepository.ExistsCodeAsync(command.FloorId, code, cancellationToken: cancellationToken))
                continue;

            var gridX = command.StartGridX + (i % 10);
            var gridY = command.StartGridY + (i / 10);

            if (await slotRepository.IsGridCellOccupiedAsync(command.FloorId, gridX, gridY, cancellationToken: cancellationToken))
                continue;

            slots.Add(new Slot
            {
                FloorId = command.FloorId,
                Code = code,
                SlotType = command.SlotType,
                MaxVehicleType = command.MaxVehicleType,
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
        }

        logger.LogInformation("Đã tạo hàng loạt {Count} slots trên Floor Id={FloorId}", slots.Count, command.FloorId);

        return slots.Count;
    }
}
