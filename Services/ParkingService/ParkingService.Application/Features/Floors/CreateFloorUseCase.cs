using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.Floors;

public interface ICreateFloorUseCase
{
    Task<FloorDto> ExecuteAsync(CreateFloorCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Tạo Tầng mới trong Khu vực (Zone).
/// </summary>
public sealed class CreateFloorUseCase(
    IFloorRepository floorRepository,
    IZoneRepository zoneRepository,
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

        logger.LogInformation("Đã tạo Floor {Name} (Level={Level}) cho Zone Id={ZoneId}",
            floor.Name, floor.Level, floor.ZoneId);

        return floor.ToDto();
    }
}
