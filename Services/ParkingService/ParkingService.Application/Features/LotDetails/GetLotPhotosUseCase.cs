using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.LotDetails;

public interface IGetLotPhotosUseCase
{
    Task<IReadOnlyList<LotPhotoDto>> ExecuteAsync(int lotId, CancellationToken cancellationToken = default);
}

public sealed class GetLotPhotosUseCase(ILotDetailsRepository repository) : IGetLotPhotosUseCase
{
    public async Task<IReadOnlyList<LotPhotoDto>> ExecuteAsync(int lotId, CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotByIdAsync(lotId, cancellationToken);
        if (lot == null)
            throw new NotFoundException("Bãi đỗ xe", lotId);

        var photos = await repository.GetPhotosByLotIdAsync(lotId, cancellationToken);

        return photos.Select(p => new LotPhotoDto(
            p.Id,
            p.ParkingLotId,
            p.Url,
            p.Caption,
            p.SortOrder,
            p.IsCover)).ToList();
    }
}
