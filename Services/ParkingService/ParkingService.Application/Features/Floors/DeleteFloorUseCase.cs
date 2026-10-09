using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.Floors;

public interface IDeleteFloorUseCase
{
    Task ExecuteAsync(int id, CancellationToken cancellationToken = default);
}

public interface IGetFloorsByZoneUseCase
{
    Task<IReadOnlyList<FloorDto>> ExecuteAsync(int zoneId, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Xóa Tầng. Chặn xóa nếu còn chứa ô đỗ.
/// </summary>
public sealed class DeleteFloorUseCase(
    IFloorRepository floorRepository,
    ISlotRepository slotRepository,
    ILogger<DeleteFloorUseCase> logger) : IDeleteFloorUseCase
{
    public async Task ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        var floor = await floorRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Tầng (Floor)", id);

        var slots = await slotRepository.GetByFloorIdAsync(id, cancellationToken);
        if (slots.Count > 0)
            throw new ConflictException($"Tầng '{floor.Name}' vẫn còn chứa {slots.Count} ô đỗ, không thể xóa.");

        await floorRepository.DeleteAsync(floor, cancellationToken);
        await floorRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Đã xóa Floor Id={Id}", id);
    }
}

public sealed class GetFloorsByZoneUseCase(IFloorRepository floorRepository) : IGetFloorsByZoneUseCase
{
    public async Task<IReadOnlyList<FloorDto>> ExecuteAsync(int zoneId, CancellationToken cancellationToken = default)
    {
        var floors = await floorRepository.GetByZoneIdAsync(zoneId, cancellationToken);
        return floors.Select(f => f.ToDto()).ToList();
    }
}
