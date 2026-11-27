using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.KybMisdeclaration;

public interface IEscalateLotMisdeclarationCaseUseCase
{
    Task<LotMisdeclarationDto> ExecuteAsync(int lotId, int caseId, EscalateLotMisdeclarationCaseRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>US-056: cảnh báo → tạm khóa (thường khi quá hạn sửa). Bãi Active → Suspended, hạn giải trình 24h tính lại.</summary>
public sealed class EscalateLotMisdeclarationCaseUseCase(ILotMisdeclarationRepository repository, ILotMisdeclarationQueries queries,
    KybMisdeclarationSettings settings, TimeProvider timeProvider) : IEscalateLotMisdeclarationCaseUseCase
{
    public async Task<LotMisdeclarationDto> ExecuteAsync(int lotId, int caseId, EscalateLotMisdeclarationCaseRequest request,
        int performedByUserId, CancellationToken cancellationToken = default)
    {
        MisdeclarationSupport.EnsureIds(lotId, performedByUserId, caseId);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var item = await MisdeclarationSupport.LoadCaseAsync(repository, lotId, caseId, cancellationToken);

        var before = MisdeclarationEvents.Snapshot(item.ParkingLot, item);
        item.Escalate(performedByUserId, request.Note ?? string.Empty, now);
        await repository.SaveWithEventsAsync(
            [MisdeclarationEvents.Audit(item.ParkingLot, MisdeclarationEvents.Suspended, performedByUserId, before,
                MisdeclarationEvents.Snapshot(item.ParkingLot, item), item.EscalationNote, now)],
            cancellationToken);
        return await MisdeclarationSupport.ReloadAsync(queries, settings, lotId, now, cancellationToken);
    }
}
