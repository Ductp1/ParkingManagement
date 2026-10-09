using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.Floors;

public interface IUpdateFloorUseCase
{
    Task<FloorDto> ExecuteAsync(UpdateFloorCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Cập nhật thông số Tầng.
/// </summary>
public sealed class UpdateFloorUseCase(
    IFloorRepository floorRepository,
    ILogger<UpdateFloorUseCase> logger) : IUpdateFloorUseCase
{
    public async Task<FloorDto> ExecuteAsync(UpdateFloorCommand command, CancellationToken cancellationToken = default)
    {
        var floor = await floorRepository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException("Tầng (Floor)", command.Id);

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ValidationException("Tên tầng không được để trống.");

        if (command.GridColumns <= 0 || command.GridRows <= 0)
            throw new ValidationException("Kích thước lưới phải lớn hơn 0.");

        var trimmedName = command.Name.Trim();
        if (await floorRepository.ExistsNameAsync(floor.ZoneId, trimmedName, excludeId: command.Id, cancellationToken))
            throw new ConflictException($"Tầng mang tên '{trimmedName}' đã tồn tại trong khu vực này.");

        floor.Name = trimmedName;
        floor.MaxHeightCm = command.MaxHeightCm;
        floor.MaxWeightKg = command.MaxWeightKg;
        floor.GridColumns = command.GridColumns;
        floor.GridRows = command.GridRows;
        floor.IsClosed = command.IsClosed;

        await floorRepository.UpdateAsync(floor, cancellationToken);
        await floorRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Đã cập nhật Floor Id={Id}", floor.Id);

        return floor.ToDto();
    }
}
