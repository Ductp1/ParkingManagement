using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.PhysicalChanges;

public interface ICompletePhysicalChangeUseCase
{
    Task<LotPhysicalChangeDto> ExecuteAsync(
        int lotId,
        int changeId,
        CompletePhysicalChangeRequest request,
        int? ownerProfileId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// USE CASE: Xác nhận hoàn tất cập nhật sơ đồ sau đợt thay đổi vật lý (US-061).
/// - Đánh giá tuân thủ SLA 48h: CompletedAtUtc <= DeadlineUtc -> Completed; > DeadlineUtc -> CompletedLate.
/// </summary>
public sealed class CompletePhysicalChangeUseCase(ILotPhysicalChangeRepository repository) : ICompletePhysicalChangeUseCase
{
    public async Task<LotPhysicalChangeDto> ExecuteAsync(
        int lotId,
        int changeId,
        CompletePhysicalChangeRequest request,
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

        var nowUtc = DateTime.UtcNow;
        change.Complete(nowUtc, request.RelatedLayoutVersionId, request.CompletionNote);

        await repository.UpdateAsync(change, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return change.ToDto(nowUtc);
    }
}
