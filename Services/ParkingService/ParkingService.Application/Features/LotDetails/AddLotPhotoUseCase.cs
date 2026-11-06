using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.LotDetails;

public interface IAddLotPhotoUseCase
{
    Task<LotPhotoDto> ExecuteAsync(
        int lotId,
        AddLotPhotoRequest request,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default);
}

public sealed class AddLotPhotoUseCase(ILotDetailsRepository repository) : IAddLotPhotoUseCase
{
    public async Task<LotPhotoDto> ExecuteAsync(
        int lotId,
        AddLotPhotoRequest request,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotByIdAsync(lotId, cancellationToken);
        if (lot == null)
            throw new NotFoundException("Bãi đỗ xe", lotId);

        if (ownerProfileId.HasValue && ownerProfileId.Value > 0 && lot.OwnerProfileId != ownerProfileId.Value)
            throw new ForbiddenException("Bạn không có quyền thao tác trên bãi xe này.");

        if (string.IsNullOrWhiteSpace(request.Url))
            throw new ValidationException("URL hình ảnh không được để trống.");

        var existingPhotos = await repository.GetPhotosByLotIdAsync(lotId, cancellationToken);
        bool shouldBeCover = request.IsCover || existingPhotos.Count == 0;

        var photo = new LotPhoto
        {
            ParkingLotId = lotId,
            Url = request.Url.Trim(),
            Caption = string.IsNullOrWhiteSpace(request.Caption) ? null : request.Caption.Trim(),
            SortOrder = request.SortOrder,
            IsCover = false
        };

        await repository.AddPhotoAsync(photo, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        if (shouldBeCover)
        {
            await repository.SetCoverPhotoAsync(lotId, photo.Id, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            photo.IsCover = true;
        }

        return new LotPhotoDto(
            photo.Id,
            photo.ParkingLotId,
            photo.Url,
            photo.Caption,
            photo.SortOrder,
            photo.IsCover);
    }
}
