using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.LotDetails;

public interface IGetLotAmenitiesUseCase
{
    Task<IReadOnlyList<LotAmenityDto>> ExecuteAsync(int lotId, CancellationToken cancellationToken = default);
}

public sealed class GetLotAmenitiesUseCase(ILotDetailsRepository repository) : IGetLotAmenitiesUseCase
{
    public async Task<IReadOnlyList<LotAmenityDto>> ExecuteAsync(int lotId, CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotByIdAsync(lotId, cancellationToken);
        if (lot == null)
            throw new NotFoundException("Bãi đỗ xe", lotId);

        var amenities = await repository.GetAmenitiesByLotIdAsync(lotId, cancellationToken);

        return amenities.Select(a => new LotAmenityDto(
            a.Id,
            a.ParkingLotId,
            a.Code,
            a.Name,
            a.Note)).ToList();
    }
}
