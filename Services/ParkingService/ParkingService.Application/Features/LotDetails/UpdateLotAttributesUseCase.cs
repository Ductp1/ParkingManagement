using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.LotDetails;

public interface IUpdateLotAttributesUseCase
{
    Task<LotAttributesDto> ExecuteAsync(
        int lotId,
        UpdateLotAttributesRequest request,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default);
}

public sealed class UpdateLotAttributesUseCase(ILotDetailsRepository repository) : IUpdateLotAttributesUseCase
{
    public async Task<LotAttributesDto> ExecuteAsync(
        int lotId,
        UpdateLotAttributesRequest request,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotWithDetailsAsync(lotId, cancellationToken);
        if (lot == null)
            throw new NotFoundException("Bãi đỗ xe", lotId);

        if (ownerProfileId.HasValue && ownerProfileId.Value > 0 && lot.OwnerProfileId != ownerProfileId.Value)
            throw new ForbiddenException("Bạn không có quyền thao tác trên bãi xe này.");

        if (request.MaxHeightCm.HasValue && request.MaxHeightCm.Value <= 0)
            throw new ValidationException("Chiều cao tĩnh không tối đa phải lớn hơn 0.");

        lot.UpdateAttributes(request.Description, request.HotlinePhone, request.MaxHeightCm);

        await repository.UpdateLotAsync(lot, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

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
