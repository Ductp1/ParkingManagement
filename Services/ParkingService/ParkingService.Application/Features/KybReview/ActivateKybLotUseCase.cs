using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.KybReview;

public interface IActivateKybLotUseCase
{
    Task<AdminKybApplicationDetailDto> ExecuteAsync(int id, ActivateKybLotRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-116 (mục 14.3): AdminService gọi sau khi hợp đồng bãi được ký. KYB Approved + bãi PendingApproval → bãi Active;
/// bãi + AuditRecorded("KybApplication.LotActivated", lý do = số hợp đồng) + ParkingLotApproved lưu trong một SaveChanges.
/// Idempotent: bãi đã Active thì không ghi gì và vẫn trả 200 (AdminService thử lại sau lỗi vẫn an toàn).
/// </summary>
public sealed class ActivateKybLotUseCase(IKybReviewRepository repository, IAdminKybQueries queries, TimeProvider timeProvider)
    : IActivateKybLotUseCase
{
    public async Task<AdminKybApplicationDetailDto> ExecuteAsync(int id, ActivateKybLotRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        KybReviewSupport.EnsureIds(id, performedByUserId);
        var contractNumber = request.ContractNumber?.Trim();
        if (string.IsNullOrEmpty(contractNumber)) throw new ValidationException("Số hợp đồng không được để trống.");
        if (contractNumber.Length > 30) throw new ValidationException("Số hợp đồng tối đa 30 ký tự.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var app = await KybReviewSupport.LoadAsync(repository, id, cancellationToken);
        var activeSlots = await repository.CountActiveSlotsAsync(app.ParkingLotId, cancellationToken);

        var before = KybReviewEvents.Snapshot(app);
        if (app.ActivateLot(app.ParkingLot, activeSlots))
        {
            await repository.SaveWithEventsAsync(
            [
                KybReviewEvents.Audit(app, "LotActivated", performedByUserId, before, contractNumber, now),
                KybReviewEvents.LotApproved(app.ParkingLot, now),
            ], cancellationToken);
        }
        return await KybReviewSupport.ReloadAsync(queries, id, cancellationToken);
    }
}
