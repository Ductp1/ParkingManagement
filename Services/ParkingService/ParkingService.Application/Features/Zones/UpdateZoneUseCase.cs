using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.Zones;

public interface IUpdateZoneUseCase
{
    Task<ZoneDto> ExecuteAsync(UpdateZoneCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Cập nhật thông tin khu vực (Zone).
/// </summary>
public sealed class UpdateZoneUseCase(
    IZoneRepository zoneRepository,
    ILogger<UpdateZoneUseCase> logger) : IUpdateZoneUseCase
{
    public async Task<ZoneDto> ExecuteAsync(UpdateZoneCommand command, CancellationToken cancellationToken = default)
    {
        var zone = await zoneRepository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException("Khu vực (Zone)", command.Id);

        if (string.IsNullOrWhiteSpace(command.Code))
            throw new ValidationException("Mã khu vực (Code) không được để trống.");

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ValidationException("Tên khu vực không được để trống.");

        var code = command.Code.Trim().ToUpperInvariant();

        if (await zoneRepository.ExistsCodeAsync(zone.ParkingLotId, code, excludeId: command.Id, cancellationToken))
            throw new ConflictException($"Mã khu vực '{code}' đã tồn tại trong bãi này.");

        zone.Code = code;
        zone.Name = command.Name.Trim();
        zone.IsOutdoor = command.IsOutdoor;
        zone.IsClosed = command.IsClosed;
        zone.ClosedReason = command.ClosedReason?.Trim();
        zone.SortOrder = command.SortOrder;

        await zoneRepository.UpdateAsync(zone, cancellationToken);
        await zoneRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Đã cập nhật Zone Id={Id}", zone.Id);

        return zone.ToDto();
    }
}
