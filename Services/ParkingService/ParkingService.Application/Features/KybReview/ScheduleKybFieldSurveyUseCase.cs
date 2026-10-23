using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.KybReview;

public interface IScheduleKybFieldSurveyUseCase
{
    Task<AdminKybApplicationDetailDto> ExecuteAsync(int id, ScheduleKybFieldSurveyRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>US-055 AC4: lên (dời) lịch khảo sát thực địa khi hồ sơ UnderReview; thời điểm phải ở tương lai.</summary>
public sealed class ScheduleKybFieldSurveyUseCase(IKybReviewRepository repository, IAdminKybQueries queries, TimeProvider timeProvider)
    : IScheduleKybFieldSurveyUseCase
{
    public async Task<AdminKybApplicationDetailDto> ExecuteAsync(int id, ScheduleKybFieldSurveyRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        KybReviewSupport.EnsureIds(id, performedByUserId);
        if (request.FieldSurveyAtUtc is not { } at) throw new ValidationException("fieldSurveyAtUtc không được để trống.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var app = await KybReviewSupport.LoadAsync(repository, id, cancellationToken);

        var before = KybReviewEvents.Snapshot(app);
        app.ScheduleFieldSurvey(KybReviewSupport.ToUtc(at), request.Note, now);
        await repository.SaveWithEventsAsync(
            [KybReviewEvents.Audit(app, "FieldSurveyScheduled", performedByUserId, before, app.FieldSurveyNote, now)], cancellationToken);
        return await KybReviewSupport.ReloadAsync(queries, id, cancellationToken);
    }
}
