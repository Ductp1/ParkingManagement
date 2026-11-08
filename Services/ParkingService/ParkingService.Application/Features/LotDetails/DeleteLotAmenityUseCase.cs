using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.LotDetails;

public interface IDeleteLotAmenityUseCase
{
    Task ExecuteAsync(
        int lotId,
        string code,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default);
}

public sealed class DeleteLotAmenityUseCase(ILotDetailsRepository repository) : IDeleteLotAmenityUseCase
{
    public async Task ExecuteAsync(
        int lotId,
        string code,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotByIdAsync(lotId, cancellationToken);
        if (lot == null)
            throw new NotFoundException("Bãi đỗ xe", lotId);

        if (ownerProfileId.HasValue && ownerProfileId.Value > 0 && lot.OwnerProfileId != ownerProfileId.Value)
            throw new ForbiddenException("Bạn không có quyền thao tác trên bãi xe này.");

        if (string.IsNullOrWhiteSpace(code))
            throw new ValidationException("Mã tiện ích không được để trống.");

        var normalizedCode = code.Trim().ToUpperInvariant();
        var amenity = await repository.GetAmenityByCodeAsync(lotId, normalizedCode, cancellationToken);
        if (amenity == null)
            throw new NotFoundException("Tiện ích", normalizedCode);

        await repository.DeleteAmenityAsync(amenity, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }
}
