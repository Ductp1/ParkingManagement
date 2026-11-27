using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.KybReview;

public interface IRequestKybMoreInfoUseCase
{
    Task<AdminKybApplicationDetailDto> ExecuteAsync(int id, RequestKybMoreInfoRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>US-055 AC3: UnderReview → NeedsMoreInfo, nội dung cần bổ sung bắt buộc; chủ bãi sửa và nộp lại (US-054).</summary>
public sealed class RequestKybMoreInfoUseCase(IKybReviewRepository repository, IAdminKybQueries queries, TimeProvider timeProvider)
    : IRequestKybMoreInfoUseCase
{
    public async Task<AdminKybApplicationDetailDto> ExecuteAsync(int id, RequestKybMoreInfoRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        KybReviewSupport.EnsureIds(id, performedByUserId);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var app = await KybReviewSupport.LoadAsync(repository, id, cancellationToken);

        var before = KybReviewEvents.Snapshot(app);
        app.RequestMoreInfo(performedByUserId, request.Note ?? string.Empty, now);
        await repository.SaveWithEventsAsync(
            [KybReviewEvents.Audit(app, "InfoRequested", performedByUserId, before, app.ReviewNote, now)], cancellationToken);
        return await KybReviewSupport.ReloadAsync(queries, id, cancellationToken);
    }
}
