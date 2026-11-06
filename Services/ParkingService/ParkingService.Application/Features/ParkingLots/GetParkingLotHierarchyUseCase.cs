using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.ParkingLots;

public sealed record GetParkingLotHierarchyQuery(int LotId);

public interface IGetParkingLotHierarchyUseCase
{
    Task<ParkingLotHierarchyDto> ExecuteAsync(GetParkingLotHierarchyQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Lấy toàn bộ cây bãi đỗ xe: Lot → Zone → Floor → Slot.
/// Đây là HỢP ĐỒNG BÀN GIAO SỐ 4 cho TV2 (App Driver), TV6 (Realtime/Availability) và TV7 (Gate Service).
/// </summary>
public sealed class GetParkingLotHierarchyUseCase(IParkingLotManagementRepository repository) : IGetParkingLotHierarchyUseCase
{
    public async Task<ParkingLotHierarchyDto> ExecuteAsync(GetParkingLotHierarchyQuery query, CancellationToken cancellationToken = default)
    {
        if (query.LotId <= 0)
            throw new ValidationException("Id bãi đỗ phải là số nguyên dương.");

        var lot = await repository.GetHierarchyAsync(query.LotId, cancellationToken);
        if (lot is null)
            throw new NotFoundException("Bãi đỗ", query.LotId);

        return lot.ToHierarchyDto();
    }
}
