using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.LotDetails;

public interface IReorderLotPhotosUseCase
{
    Task<IReadOnlyList<LotPhotoDto>> ExecuteAsync(
        int lotId,
        ReorderLotPhotosRequest request,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default);
}

public sealed class ReorderLotPhotosUseCase(ILotDetailsRepository repository) : IReorderLotPhotosUseCase
{
    public async Task<IReadOnlyList<LotPhotoDto>> ExecuteAsync(
        int lotId,
        ReorderLotPhotosRequest request,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotByIdAsync(lotId, cancellationToken);
        if (lot == null)
            throw new NotFoundException("Bãi đỗ xe", lotId);

        if (ownerProfileId.HasValue && ownerProfileId.Value > 0 && lot.OwnerProfileId != ownerProfileId.Value)
            throw new ForbiddenException("Bạn không có quyền thao tác trên bãi xe này.");

        if (request.Items == null || request.Items.Count == 0)
            throw new ValidationException("Danh sách sắp xếp ảnh không được để trống.");

        var dict = request.Items.ToDictionary(i => i.PhotoId, i => i.SortOrder);

        await repository.ReorderPhotosAsync(lotId, dict, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

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
