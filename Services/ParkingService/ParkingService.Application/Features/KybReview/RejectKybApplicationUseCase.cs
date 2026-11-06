using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.KybReview;

public interface IRejectKybApplicationUseCase
{
    Task<AdminKybApplicationDetailDto> ExecuteAsync(int id, RejectKybApplicationRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>US-055 AC3: UnderReview → Rejected, lý do bắt buộc; bãi giữ PendingApproval, chủ bãi được nộp lại (US-054).</summary>
public sealed class RejectKybApplicationUseCase(IKybReviewRepository repository, IAdminKybQueries queries, TimeProvider timeProvider)
    : IRejectKybApplicationUseCase
{
    public async Task<AdminKybApplicationDetailDto> ExecuteAsync(int id, RejectKybApplicationRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        KybReviewSupport.EnsureIds(id, performedByUserId);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var app = await KybReviewSupport.LoadAsync(repository, id, cancellationToken);

        var before = KybReviewEvents.Snapshot(app);
        app.Reject(performedByUserId, request.Reason ?? string.Empty, now);
        await repository.SaveWithEventsAsync(
            [KybReviewEvents.Audit(app, "Rejected", performedByUserId, before, app.RejectReason, now)], cancellationToken);
        return await KybReviewSupport.ReloadAsync(queries, id, cancellationToken);
    }
}
