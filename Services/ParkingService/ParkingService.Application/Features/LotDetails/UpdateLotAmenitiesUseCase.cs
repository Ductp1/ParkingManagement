using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.LotDetails;

public interface IUpdateLotAmenitiesUseCase
{
    Task<IReadOnlyList<LotAmenityDto>> ExecuteAsync(
        int lotId,
        UpdateLotAmenitiesRequest request,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default);
}

public sealed class UpdateLotAmenitiesUseCase(ILotDetailsRepository repository) : IUpdateLotAmenitiesUseCase
{
    public async Task<IReadOnlyList<LotAmenityDto>> ExecuteAsync(
        int lotId,
        UpdateLotAmenitiesRequest request,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotByIdAsync(lotId, cancellationToken);
        if (lot == null)
            throw new NotFoundException("Bãi đỗ xe", lotId);

        if (ownerProfileId.HasValue && ownerProfileId.Value > 0 && lot.OwnerProfileId != ownerProfileId.Value)
            throw new ForbiddenException("Bạn không có quyền thao tác trên bãi xe này.");

        var inputList = request.Amenities ?? [];

        // Validate codes
        foreach (var item in inputList)
        {
            if (string.IsNullOrWhiteSpace(item.Code))
                throw new ValidationException("Mã tiện ích không được để trống.");
        }

        var duplicates = inputList
            .GroupBy(x => x.Code.Trim().ToUpperInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
            throw new ConflictException($"Mã tiện ích bị trùng lặp trong yêu cầu: {string.Join(", ", duplicates)}.");

        var newAmenities = inputList.Select(item =>
        {
            var code = item.Code.Trim().ToUpperInvariant();
            var name = string.IsNullOrWhiteSpace(item.Name)
                ? StandardAmenitiesCatalog.GetDefaultName(code)
                : item.Name.Trim();

            return new LotAmenity
            {
                ParkingLotId = lotId,
                Code = code,
                Name = name,
                Note = string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim()
            };
        }).ToList();

        await repository.ReplaceAmenitiesAsync(lotId, newAmenities, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        var savedAmenities = await repository.GetAmenitiesByLotIdAsync(lotId, cancellationToken);
        return savedAmenities.Select(a => new LotAmenityDto(
            a.Id,
            a.ParkingLotId,
            a.Code,
            a.Name,
            a.Note)).ToList();
    }
}
