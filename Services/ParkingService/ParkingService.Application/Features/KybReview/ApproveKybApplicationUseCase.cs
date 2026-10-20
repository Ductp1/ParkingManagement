using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.KybReview;

public interface IApproveKybApplicationUseCase
{
    Task<AdminKybApplicationDetailDto> ExecuteAsync(int id, ApproveKybApplicationRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-055 AC2: UnderReview → Approved, bãi PendingApproval → Active. Hồ sơ + bãi + AuditRecorded + ParkingLotApproved
/// (NotificationService báo chủ bãi) lưu trong một SaveChanges.
/// </summary>
public sealed class ApproveKybApplicationUseCase(IKybReviewRepository repository, IAdminKybQueries queries, TimeProvider timeProvider)
    : IApproveKybApplicationUseCase
{
    public async Task<AdminKybApplicationDetailDto> ExecuteAsync(int id, ApproveKybApplicationRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        KybReviewSupport.EnsureIds(id, performedByUserId);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var app = await KybReviewSupport.LoadAsync(repository, id, cancellationToken);
        var activeSlots = await repository.CountActiveSlotsAsync(app.ParkingLotId, cancellationToken);

        var before = KybReviewEvents.Snapshot(app);
        app.Approve(performedByUserId, app.ParkingLot, activeSlots, request.Note, now);
        await repository.SaveWithEventsAsync(
        [
            KybReviewEvents.Audit(app, "Approved", performedByUserId, before, app.ReviewNote, now),
            KybReviewEvents.LotApproved(app.ParkingLot, now),
        ], cancellationToken);
        return await KybReviewSupport.ReloadAsync(queries, id, cancellationToken);
    }
}
