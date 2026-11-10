using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.PhysicalChanges;

public interface ICancelPhysicalChangeUseCase
{
    Task<LotPhysicalChangeDto> ExecuteAsync(
        int lotId,
        int changeId,
        CancelPhysicalChangeRequest request,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Hủy đợt thay đổi vật lý khi chủ bãi hoãn hoặc không thực hiện thi công (US-061).
/// </summary>
public sealed class CancelPhysicalChangeUseCase(ILotPhysicalChangeRepository repository) : ICancelPhysicalChangeUseCase
{
    public async Task<LotPhysicalChangeDto> ExecuteAsync(
        int lotId,
        int changeId,
        CancelPhysicalChangeRequest request,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default)
    {
        var lot = await repository.GetLotByIdAsync(lotId, cancellationToken);
        if (lot == null)
            throw new NotFoundException("Bãi đỗ xe", lotId);

        if (ownerProfileId.HasValue && ownerProfileId.Value > 0 && lot.OwnerProfileId != ownerProfileId.Value)
            throw new ForbiddenException("Bạn không có quyền thao tác trên bãi xe này.");

        var change = await repository.GetByIdAsync(changeId, cancellationToken);
        if (change == null || change.ParkingLotId != lotId)
            throw new NotFoundException("Đợt thay đổi vật lý", changeId);

        change.Cancel(request.Reason);

        await repository.UpdateAsync(change, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return change.ToDto(DateTime.UtcNow);
    }
}
