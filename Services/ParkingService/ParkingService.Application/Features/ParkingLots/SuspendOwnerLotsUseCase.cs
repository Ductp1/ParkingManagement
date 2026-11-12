using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.ParkingLots;

public interface ISuspendOwnerLotsUseCase
{
    Task<SuspendOwnerLotsResultDto> ExecuteAsync(int ownerProfileId, CancellationToken cancellationToken = default);
}

public sealed class SuspendOwnerLotsUseCase(
    IParkingLotManagementRepository repository,
    TimeProvider timeProvider,
    ILogger<SuspendOwnerLotsUseCase> logger) : ISuspendOwnerLotsUseCase
{
    public async Task<SuspendOwnerLotsResultDto> ExecuteAsync(int ownerProfileId, CancellationToken cancellationToken = default)
    {
        var lots = await repository.GetByOwnerProfileIdAsync(ownerProfileId, cancellationToken);
        var suspendedLotIds = new List<int>();

        foreach (var lot in lots)
        {
            if (lot.Status != ParkingLotStatus.Suspended)
            {
                lot.Suspend();
                suspendedLotIds.Add(lot.Id);
            }
        }

        if (suspendedLotIds.Count > 0)
        {
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Đã đình chỉ {Count} bãi đỗ của chủ bãi {OwnerProfileId} do rời nền tảng (Ids: {Ids})",
                suspendedLotIds.Count, ownerProfileId, string.Join(", ", suspendedLotIds));
        }

        return new SuspendOwnerLotsResultDto(
            ownerProfileId,
            suspendedLotIds.Count,
            suspendedLotIds,
            timeProvider.GetUtcNow().UtcDateTime);
    }
}
