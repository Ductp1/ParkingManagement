using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.Slots;

public interface IDeleteSlotUseCase
{
    Task ExecuteAsync(int id, CancellationToken cancellationToken = default);
}

public interface IGetSlotsByFloorUseCase
{
    Task<IReadOnlyList<SlotDto>> ExecuteAsync(int floorId, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Xóa ô đỗ. Chặn xóa nếu ô đỗ đang có xe hoặc đang bị giữ chỗ.
/// </summary>
public sealed class DeleteSlotUseCase(
    ISlotRepository slotRepository,
    ILogger<DeleteSlotUseCase> logger) : IDeleteSlotUseCase
{
    public async Task ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        var slot = await slotRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Ô đỗ (Slot)", id);

        // Quy tắc nghiệp vụ: Slot đang Reserved hoặc Occupied thì TUYỆT ĐỐI không được xóa
        if (slot.State != SlotState.Available)
        {
            logger.LogWarning("Không thể xóa Slot Id={Id} vì trạng thái là {State}", id, slot.State);
            throw new ConflictException($"Ô đỗ '{slot.Code}' đang ở trạng thái '{slot.State}', không thể xóa.");
        }

        await slotRepository.DeleteAsync(slot, cancellationToken);
        await slotRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Đã xóa Slot Id={Id}", id);
    }
}

public sealed class GetSlotsByFloorUseCase(ISlotRepository slotRepository) : IGetSlotsByFloorUseCase
{
    public async Task<IReadOnlyList<SlotDto>> ExecuteAsync(int floorId, CancellationToken cancellationToken = default)
    {
        var slots = await slotRepository.GetByFloorIdAsync(floorId, cancellationToken);
        return slots.Select(s => s.ToDto()).ToList();
    }
}
