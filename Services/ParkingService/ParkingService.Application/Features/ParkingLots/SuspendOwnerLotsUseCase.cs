using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.LotOperations;

namespace ParkingService.Application.Features.ParkingLots;

public interface ISuspendOwnerLotsUseCase
{
    Task<SuspendOwnerLotsResultDto> ExecuteAsync(int ownerProfileId, CancellationToken cancellationToken = default);
}

public sealed class SuspendOwnerLotsUseCase(
    IParkingLotManagementRepository repository,
    TimeProvider timeProvider,
    ILogger<SuspendOwnerLotsUseCase> logger,
    ILotBookingLookup? bookingLookup = null) : ISuspendOwnerLotsUseCase
{
    public async Task<SuspendOwnerLotsResultDto> ExecuteAsync(int ownerProfileId, CancellationToken cancellationToken = default)
    {
        var lots = await repository.GetByOwnerProfileIdAsync(ownerProfileId, cancellationToken);
        var suspendedLotIds = new List<int>();
        var totalAffected = 0;

        foreach (var lot in lots)
        {
            if (lot.Status != ParkingLotStatus.Suspended)
            {
                if (bookingLookup is not null)
                {
                    try
                    {
                        var affected = await bookingLookup.GetAffectedBookingsAsync(lot.Id, cancellationToken);
                        totalAffected += affected.Count;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Không thể tải danh sách booking bị ảnh hưởng khi đình chỉ bãi {LotId}", lot.Id);
                    }
                }

                lot.Suspend();
                suspendedLotIds.Add(lot.Id);
            }
        }

        if (suspendedLotIds.Count > 0)
        {
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Đã đình chỉ {Count} bãi đỗ của chủ bãi {OwnerProfileId} do rời nền tảng (Ids: {Ids}, AffectedBookings: {Affected})",
                suspendedLotIds.Count, ownerProfileId, string.Join(", ", suspendedLotIds), totalAffected);
        }

        return new SuspendOwnerLotsResultDto(
            ownerProfileId,
            suspendedLotIds.Count,
            suspendedLotIds,
            timeProvider.GetUtcNow().UtcDateTime,
            totalAffected);
    }
}
