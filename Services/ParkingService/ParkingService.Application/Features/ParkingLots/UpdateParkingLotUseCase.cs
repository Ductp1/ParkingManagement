using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.ParkingLots;

public interface IUpdateParkingLotUseCase
{
    Task ExecuteAsync(UpdateParkingLotCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Cập nhật thông tin bãi đỗ xe.
/// </summary>
public sealed class UpdateParkingLotUseCase(
    IParkingLotManagementRepository repository,
    ILogger<UpdateParkingLotUseCase> logger) : IUpdateParkingLotUseCase
{
    public async Task ExecuteAsync(UpdateParkingLotCommand command, CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ", command.Id);

        if (lot.OwnerProfileId != command.OwnerProfileId)
            throw new ConflictException("Bạn không có quyền chỉnh sửa bãi đỗ này.");

        if (await repository.ExistsByNameAsync(command.OwnerProfileId, command.Name.Trim(), excludeId: command.Id, cancellationToken))
            throw new ConflictException($"Chủ bãi đã có bãi đỗ khác mang tên '{command.Name.Trim()}'.");

        lot.UpdateDetails(
            name: command.Name.Trim(),
            address: command.Address.Trim(),
            description: command.Description?.Trim(),
            hotlinePhone: command.HotlinePhone?.Trim(),
            maxHeightCm: command.MaxHeightCm,
            openTime: command.OpenTime,
            closeTime: command.CloseTime,
            city: command.City.Trim(),
            district: command.District?.Trim());

        await repository.UpdateAsync(lot, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Cập nhật thông tin bãi đỗ Id={Id} thành công", command.Id);
    }
}
