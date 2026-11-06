using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.Zones;

public interface IDeleteZoneUseCase
{
    Task ExecuteAsync(int id, CancellationToken cancellationToken = default);
}

public interface IGetZonesByLotUseCase
{
    Task<IReadOnlyList<ZoneDto>> ExecuteAsync(int parkingLotId, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Xóa khu vực (Zone). Chặn xóa nếu còn chứa tầng con.
/// </summary>
public sealed class DeleteZoneUseCase(
    IZoneRepository zoneRepository,
    IFloorRepository floorRepository,
    ILogger<DeleteZoneUseCase> logger) : IDeleteZoneUseCase
{
    public async Task ExecuteAsync(int id, CancellationToken cancellationToken = default)
    {
        var zone = await zoneRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Khu vực (Zone)", id);

        var floors = await floorRepository.GetByZoneIdAsync(id, cancellationToken);
        if (floors.Count > 0)
            throw new ConflictException($"Khu vực '{zone.Name}' vẫn còn chứa {floors.Count} tầng con, không thể xóa.");

        await zoneRepository.DeleteAsync(zone, cancellationToken);
        await zoneRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Đã xóa Zone Id={Id}", id);
    }
}

public sealed class GetZonesByLotUseCase(IZoneRepository zoneRepository) : IGetZonesByLotUseCase
{
    public async Task<IReadOnlyList<ZoneDto>> ExecuteAsync(int parkingLotId, CancellationToken cancellationToken = default)
    {
        var zones = await zoneRepository.GetByParkingLotIdAsync(parkingLotId, cancellationToken);
        return zones.Select(z => z.ToDto()).ToList();
    }
}
