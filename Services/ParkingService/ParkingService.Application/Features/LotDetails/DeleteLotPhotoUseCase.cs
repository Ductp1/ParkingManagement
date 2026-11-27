using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.LotDetails;

public interface IDeleteLotPhotoUseCase
{
    Task ExecuteAsync(
        int lotId,
        int photoId,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default);
}

public sealed class DeleteLotPhotoUseCase(ILotDetailsRepository repository) : IDeleteLotPhotoUseCase
{
    public async Task ExecuteAsync(
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

        bool wasCover = photo.IsCover;

        await repository.DeletePhotoAsync(photo, cancellationToken);

        if (wasCover)
        {
            var remainingPhotos = (await repository.GetPhotosByLotIdAsync(lotId, cancellationToken))
                .Where(p => p.Id != photoId)
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Id)
                .ToList();

            if (remainingPhotos.Count > 0)
            {
                var nextCover = remainingPhotos[0];
                await repository.SetCoverPhotoAsync(lotId, nextCover.Id, cancellationToken);
            }
            else
            {
                lot.SetCoverImageUrl(null);
                await repository.UpdateLotAsync(lot, cancellationToken);
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
    }
}
