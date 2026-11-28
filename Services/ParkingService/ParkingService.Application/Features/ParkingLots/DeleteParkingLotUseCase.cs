using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.ParkingLots;

public interface IDeleteParkingLotUseCase
{
    Task ExecuteAsync(int lotId, int ownerProfileId, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Xóa bãi đỗ xe thuộc quyền sở hữu của chủ bãi (Soft delete).
/// </summary>
public sealed class DeleteParkingLotUseCase(
    IParkingLotManagementRepository repository,
    TimeProvider timeProvider,
    ILogger<DeleteParkingLotUseCase> logger) : IDeleteParkingLotUseCase
{
    public async Task ExecuteAsync(int lotId, int ownerProfileId, CancellationToken cancellationToken = default)
    {
        if (lotId <= 0)
            throw new ValidationException("LotId không hợp lệ.");

        if (ownerProfileId <= 0)
            throw new ValidationException("OwnerProfileId không hợp lệ.");

        var lot = await repository.GetByIdAsync(lotId, cancellationToken)
            ?? throw new NotFoundException("Bãi đỗ", lotId);

        if (lot.OwnerProfileId != ownerProfileId)
            throw new ConflictException("Bạn không có quyền xóa bãi đỗ xe này.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        lot.Delete(now);

        await repository.UpdateAsync(lot, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Chủ bãi {OwnerProfileId} đã xóa thành công bãi đỗ Id={LotId} ({LotName})",
            ownerProfileId, lotId, lot.Name);
    }
}
