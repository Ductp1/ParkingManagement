using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Application.Abstractions;

namespace ParkingService.Application.Features.KybMisdeclaration;

public interface IResolveLotMisdeclarationCaseUseCase
{
    Task<LotMisdeclarationDto> ExecuteAsync(int lotId, int caseId, ResolveLotMisdeclarationCaseRequest request, int performedByUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-056: chủ bãi đã sửa đúng / giải trình hợp lệ → đóng case. Bãi đang tạm khóa → khôi phục Active
/// (audit MisdeclarationResolved + Reinstated trong cùng SaveChanges).
/// </summary>
public sealed class ResolveLotMisdeclarationCaseUseCase(ILotMisdeclarationRepository repository, ILotMisdeclarationQueries queries,
    KybMisdeclarationSettings settings, TimeProvider timeProvider) : IResolveLotMisdeclarationCaseUseCase
{
    public async Task<LotMisdeclarationDto> ExecuteAsync(int lotId, int caseId, ResolveLotMisdeclarationCaseRequest request,
        int performedByUserId, CancellationToken cancellationToken = default)
    {
        MisdeclarationSupport.EnsureIds(lotId, performedByUserId, caseId);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var item = await MisdeclarationSupport.LoadCaseAsync(repository, lotId, caseId, cancellationToken);
        var lot = item.ParkingLot;

        var before = MisdeclarationEvents.Snapshot(lot, item);
        var reinstated = item.Resolve(performedByUserId, request.Note ?? string.Empty, now);
        var after = MisdeclarationEvents.Snapshot(lot, item);

        List<IntegrationEvent> events =
            [MisdeclarationEvents.Audit(lot, MisdeclarationEvents.Resolved, performedByUserId, before, after, item.ResolutionNote, now)];
        if (reinstated)
            events.Add(MisdeclarationEvents.Audit(lot, MisdeclarationEvents.Reinstated, performedByUserId, before, after,
                item.ResolutionNote, now));
        await repository.SaveWithEventsAsync(events, cancellationToken);
        return await MisdeclarationSupport.ReloadAsync(queries, settings, lotId, now, cancellationToken);
    }
}
