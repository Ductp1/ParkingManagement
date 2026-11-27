using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.PhysicalChanges;

public interface IGetPhysicalChangeByIdUseCase
{
    Task<LotPhysicalChangeDto> ExecuteAsync(int lotId, int changeId, CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Xem chi tiết một đợt thay đổi vật lý và đồng hồ đếm ngược SLA 48h (Tiêu chí 1, US-061).
/// </summary>
public sealed class GetPhysicalChangeByIdUseCase(ILotPhysicalChangeRepository repository) : IGetPhysicalChangeByIdUseCase
{
    public async Task<LotPhysicalChangeDto> ExecuteAsync(int lotId, int changeId, CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotByIdAsync(lotId, cancellationToken);
        if (lot == null)
            throw new NotFoundException("Bãi đỗ xe", lotId);

        var change = await repository.GetByIdAsync(changeId, cancellationToken);
        if (change == null || change.ParkingLotId != lotId)
            throw new NotFoundException("Đợt thay đổi vật lý", changeId);

        var nowUtc = DateTime.UtcNow;
        if (change.CheckAndMarkOverdue(nowUtc))
        {
            await repository.UpdateAsync(change, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
        }

        return change.ToDto(nowUtc);
    }
}
