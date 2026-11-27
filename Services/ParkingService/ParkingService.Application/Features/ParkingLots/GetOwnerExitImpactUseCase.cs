using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.ParkingLots;

public interface IGetOwnerExitImpactUseCase
{
    Task<OwnerExitImpactSummaryDto> ExecuteAsync(int ownerProfileId, CancellationToken cancellationToken = default);
}

public sealed class GetOwnerExitImpactUseCase(IParkingLotManagementRepository repository) : IGetOwnerExitImpactUseCase
{
    public async Task<OwnerExitImpactSummaryDto> ExecuteAsync(int ownerProfileId, CancellationToken cancellationToken = default)
    {
        var lots = await repository.GetByOwnerProfileIdAsync(ownerProfileId, cancellationToken);

        var lotDtos = lots.Select(l => new OwnerLotSummaryItemDto(
            l.Id,
            l.Name,
            l.Address,
            l.Status.ToString(),
            l.TotalSlots,
            l.AvailableSlots)).ToList();

        var totalLots = lotDtos.Count;
        var activeLots = lots.Count(l => l.Status == ParkingLotStatus.Active);
        var suspendedLots = lots.Count(l => l.Status == ParkingLotStatus.Suspended);
        var totalSlots = lots.Sum(l => l.TotalSlots);

        return new OwnerExitImpactSummaryDto(
            ownerProfileId,
            totalLots,
            activeLots,
            suspendedLots,
            totalSlots,
            lotDtos);
    }
}
