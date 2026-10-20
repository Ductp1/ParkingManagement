using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.KybReview;

public interface IRecordKybFieldSurveyResultUseCase
{
    Task<AdminKybApplicationDetailDto> ExecuteAsync(int id, RecordKybFieldSurveyResultRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>US-055 AC4: Admin xác nhận đã khảo sát thực địa (sau giờ hẹn) kèm kết quả – điều kiện duyệt bãi trên 50 chỗ.</summary>
public sealed class RecordKybFieldSurveyResultUseCase(IKybReviewRepository repository, IAdminKybQueries queries, TimeProvider timeProvider)
    : IRecordKybFieldSurveyResultUseCase
{
    public async Task<AdminKybApplicationDetailDto> ExecuteAsync(int id, RecordKybFieldSurveyResultRequest request, int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        KybReviewSupport.EnsureIds(id, performedByUserId);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var app = await KybReviewSupport.LoadAsync(repository, id, cancellationToken);

        var before = KybReviewEvents.Snapshot(app);
        app.RecordFieldSurveyResult(request.Note ?? string.Empty, now);
        await repository.SaveWithEventsAsync(
            [KybReviewEvents.Audit(app, "FieldSurveyCompleted", performedByUserId, before, app.FieldSurveyNote, now)], cancellationToken);
        return await KybReviewSupport.ReloadAsync(queries, id, cancellationToken);
    }
}
