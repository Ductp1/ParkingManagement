using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.LotDetails;

public interface IAddLotAmenityUseCase
{
    Task<LotAmenityDto> ExecuteAsync(
        int lotId,
        LotAmenityInputDto request,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default);
}

public sealed class AddLotAmenityUseCase(ILotDetailsRepository repository) : IAddLotAmenityUseCase
{
    public async Task<LotAmenityDto> ExecuteAsync(
        int lotId,
        LotAmenityInputDto request,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotByIdAsync(lotId, cancellationToken);
        if (lot == null)
            throw new NotFoundException("Bãi đỗ xe", lotId);

        if (ownerProfileId.HasValue && ownerProfileId.Value > 0 && lot.OwnerProfileId != ownerProfileId.Value)
            throw new ForbiddenException("Bạn không có quyền thao tác trên bãi xe này.");

        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ValidationException("Mã tiện ích không được để trống.");

        var code = request.Code.Trim().ToUpperInvariant();

        var existing = await repository.GetAmenityByCodeAsync(lotId, code, cancellationToken);
        if (existing != null)
            throw new ConflictException($"Tiện ích có mã '{code}' đã tồn tại trong bãi xe.");

        var name = string.IsNullOrWhiteSpace(request.Name)
            ? StandardAmenitiesCatalog.GetDefaultName(code)
            : request.Name.Trim();

        var amenity = new LotAmenity
        {
            ParkingLotId = lotId,
            Code = code,
            Name = name,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        };

        await repository.AddAmenityAsync(amenity, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return new LotAmenityDto(
            amenity.Id,
            amenity.ParkingLotId,
            amenity.Code,
            amenity.Name,
            amenity.Note);
    }
}
