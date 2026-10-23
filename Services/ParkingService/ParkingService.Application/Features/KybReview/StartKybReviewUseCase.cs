using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.KybReview;

public interface IStartKybReviewUseCase
{
    Task<AdminKybApplicationDetailDto> ExecuteAsync(int id, int performedByUserId, CancellationToken cancellationToken = default);
}

/// <summary>US-055: Submitted → UnderReview, người thẩm định = Admin trong token. Audit qua outbox cùng SaveChanges.</summary>
public sealed class StartKybReviewUseCase(IKybReviewRepository repository, IAdminKybQueries queries, TimeProvider timeProvider)
    : IStartKybReviewUseCase
{
    public async Task<AdminKybApplicationDetailDto> ExecuteAsync(int id, int performedByUserId, CancellationToken cancellationToken = default)
    {
        KybReviewSupport.EnsureIds(id, performedByUserId);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var app = await KybReviewSupport.LoadAsync(repository, id, cancellationToken);

        var before = KybReviewEvents.Snapshot(app);
        app.StartReview(performedByUserId);
        await repository.SaveWithEventsAsync([KybReviewEvents.Audit(app, "ReviewStarted", performedByUserId, before, null, now)],
            cancellationToken);
        return await KybReviewSupport.ReloadAsync(queries, id, cancellationToken);
    }
}
