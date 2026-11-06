using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.LotDetails;

public interface IGetLotAttributesUseCase
{
    Task<LotAttributesDto> ExecuteAsync(int lotId, CancellationToken cancellationToken = default);
}

public sealed class GetLotAttributesUseCase(ILotDetailsRepository repository) : IGetLotAttributesUseCase
{
    public async Task<LotAttributesDto> ExecuteAsync(int lotId, CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotWithDetailsAsync(lotId, cancellationToken);
        if (lot == null)
            throw new NotFoundException("Bãi đỗ xe", lotId);

        var amenities = lot.Amenities
            .OrderBy(a => a.Code)
            .Select(a => new LotAmenityDto(
                a.Id,
                a.ParkingLotId,
                a.Code,
                a.Name,
                a.Note))
            .ToList();

        var photos = lot.Photos
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .Select(p => new LotPhotoDto(
                p.Id,
                p.ParkingLotId,
                p.Url,
                p.Caption,
                p.SortOrder,
                p.IsCover))
            .ToList();

        return new LotAttributesDto(
            lot.Id,
            lot.Name,
            lot.Description,
            lot.HotlinePhone,
            lot.MaxHeightCm,
            lot.CoverImageUrl,
            amenities,
            photos);
    }
}
