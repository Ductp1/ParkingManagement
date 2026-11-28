using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.LotOperations;

namespace ParkingService.Application.Features.ParkingLots;

public interface IGetOwnerExitImpactUseCase
{
    Task<OwnerExitImpactSummaryDto> ExecuteAsync(int ownerProfileId, CancellationToken cancellationToken = default);
}

public sealed class GetOwnerExitImpactUseCase(
    IParkingLotManagementRepository repository,
    ILotBookingLookup? bookingLookup = null) : IGetOwnerExitImpactUseCase
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

        var upcomingBookings = 0;
        var activeBookings = 0;
        if (bookingLookup is not null)
        {
            foreach (var lot in lots)
            {
                try
                {
                    var (active, upcoming) = await bookingLookup.GetBookingCountsAsync(lot.Id, cancellationToken);
                    activeBookings += active;
                    upcomingBookings += upcoming;
                }
                catch
                {
                    // Degrade gracefully if booking service lookup fails
                }
            }
        }

        return new OwnerExitImpactSummaryDto(
            ownerProfileId,
            totalLots,
            activeLots,
            suspendedLots,
            totalSlots,
            lotDtos,
            upcomingBookings,
            activeBookings);
    }
}
