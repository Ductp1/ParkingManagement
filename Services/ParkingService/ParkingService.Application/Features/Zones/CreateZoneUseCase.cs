using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Zones;

public interface ICreateZoneUseCase
{
    Task<ZoneDto> ExecuteAsync(CreateZoneCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Tạo khu vực / tòa nhà (Zone) mới trong bãi đỗ (US-058).
/// </summary>
public sealed class CreateZoneUseCase(
    IZoneRepository zoneRepository,
    IParkingLotManagementRepository lotRepository,
    ILogger<CreateZoneUseCase> logger) : ICreateZoneUseCase
{
    public async Task<ZoneDto> ExecuteAsync(CreateZoneCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ParkingLotId <= 0)
            throw new ValidationException("ParkingLotId không hợp lệ.");

        if (string.IsNullOrWhiteSpace(command.Code))
            throw new ValidationException("Mã khu vực (Code) không được để trống.");

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ValidationException("Tên khu vực không được để trống.");

        var lot = await lotRepository.GetByIdAsync(command.ParkingLotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ", command.ParkingLotId);

        if (await zoneRepository.ExistsCodeAsync(command.ParkingLotId, command.Code.Trim().ToUpperInvariant(), cancellationToken: cancellationToken))
            throw new ConflictException($"Mã khu vực '{command.Code.Trim().ToUpperInvariant()}' đã tồn tại trong bãi này.");

        var zone = new Zone
        {
            ParkingLotId = command.ParkingLotId,
            Code = command.Code.Trim().ToUpperInvariant(),
            Name = command.Name.Trim(),
            IsOutdoor = command.IsOutdoor,
            SortOrder = command.SortOrder
        };

        await zoneRepository.AddAsync(zone, cancellationToken);
        await zoneRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Đã tạo Zone {Code} cho bãi Id={LotId}", zone.Code, zone.ParkingLotId);

        return zone.ToDto();
    }
}
