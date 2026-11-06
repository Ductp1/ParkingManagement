using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.ParkingLots;

public interface IGetOwnerParkingLotsUseCase
{
    Task<IReadOnlyList<OwnerParkingLotItemDto>> ExecuteAsync(int ownerProfileId, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Lấy danh sách các bãi đỗ thuộc quyền sở hữu của chủ bãi (Owner Portal).
/// </summary>
public sealed class GetOwnerParkingLotsUseCase(IParkingLotManagementRepository repository) : IGetOwnerParkingLotsUseCase
{
    public async Task<IReadOnlyList<OwnerParkingLotItemDto>> ExecuteAsync(int ownerProfileId, CancellationToken cancellationToken = default)
    {
        if (ownerProfileId <= 0)
            throw new ValidationException("OwnerProfileId không hợp lệ.");

        var lots = await repository.GetByOwnerProfileIdAsync(ownerProfileId, cancellationToken);
        return lots.Select(l => l.ToOwnerItemDto()).ToList();
    }
}
