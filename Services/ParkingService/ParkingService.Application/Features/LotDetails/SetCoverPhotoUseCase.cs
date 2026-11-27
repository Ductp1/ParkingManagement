using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.LotDetails;

public interface ISetCoverPhotoUseCase
{
    Task<LotPhotoDto> ExecuteAsync(
        int lotId,
        int photoId,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default);
}

public sealed class SetCoverPhotoUseCase(ILotDetailsRepository repository) : ISetCoverPhotoUseCase
{
    public async Task<LotPhotoDto> ExecuteAsync(
        int lotId,
        int photoId,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotByIdAsync(lotId, cancellationToken);
        if (lot == null)
            throw new NotFoundException("Bãi đỗ xe", lotId);

        if (ownerProfileId.HasValue && ownerProfileId.Value > 0 && lot.OwnerProfileId != ownerProfileId.Value)
            throw new ForbiddenException("Bạn không có quyền thao tác trên bãi xe này.");

        var photo = await repository.GetPhotoByIdAsync(photoId, cancellationToken);
        if (photo == null || photo.ParkingLotId != lotId)
            throw new NotFoundException("Ảnh bãi xe", photoId);

        await repository.SetCoverPhotoAsync(lotId, photoId, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        var updatedPhoto = await repository.GetPhotoByIdAsync(photoId, cancellationToken) ?? photo;

        return new LotPhotoDto(
            updatedPhoto.Id,
            updatedPhoto.ParkingLotId,
            updatedPhoto.Url,
            updatedPhoto.Caption,
            updatedPhoto.SortOrder,
            IsCover: true);
    }
}
