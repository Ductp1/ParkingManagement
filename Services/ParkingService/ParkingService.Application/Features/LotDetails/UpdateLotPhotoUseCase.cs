using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.LotDetails;

public interface IUpdateLotPhotoUseCase
{
    Task<LotPhotoDto> ExecuteAsync(
        int lotId,
        int photoId,
        UpdateLotPhotoRequest request,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default);
}

public sealed class UpdateLotPhotoUseCase(ILotDetailsRepository repository) : IUpdateLotPhotoUseCase
{
    public async Task<LotPhotoDto> ExecuteAsync(
        int lotId,
        int photoId,
        UpdateLotPhotoRequest request,
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

        if (request.Caption != null)
        {
            photo.Caption = string.IsNullOrWhiteSpace(request.Caption) ? null : request.Caption.Trim();
        }

        if (request.SortOrder.HasValue)
        {
            photo.SortOrder = request.SortOrder.Value;
        }

        await repository.UpdatePhotoAsync(photo, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return new LotPhotoDto(
            photo.Id,
            photo.ParkingLotId,
            photo.Url,
            photo.Caption,
            photo.SortOrder,
            photo.IsCover);
    }
}
