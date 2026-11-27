using ParkingService.Domain.Enums;

namespace ParkingService.Application.Features.SanctionStates;

public interface IGetLotSanctionStateUseCase
{
    Task<LotSanctionStateDto> ExecuteAsync(int parkingLotId, CancellationToken cancellationToken = default);
}

/// <summary>US-097: trạng thái chế tài đầy đủ của bãi cho Admin (tạm ngưng + hạ thứ hạng, theo bãi và theo chủ bãi).</summary>
public sealed class GetLotSanctionStateUseCase(Abstractions.ISanctionStateQueries queries, TimeProvider timeProvider)
    : IGetLotSanctionStateUseCase
{
    public async Task<LotSanctionStateDto> ExecuteAsync(int parkingLotId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var (lot, effects) = await LotSanctionLoader.LoadAsync(queries, parkingLotId, now, cancellationToken);
        var suspended = effects.IsListingSuspended(lot.Id, lot.OwnerProfileId);
        var penalized = effects.IsRankingPenalized(lot.Id, lot.OwnerProfileId);
        return new LotSanctionStateDto(lot.Id, lot.OwnerProfileId,
            suspended, suspended ? effects.Until(lot.Id, lot.OwnerProfileId, SanctionStateKind.ListingSuspension) : null,
            penalized, penalized ? effects.Until(lot.Id, lot.OwnerProfileId, SanctionStateKind.RankingPenalty) : null,
            effects.For(lot.Id, lot.OwnerProfileId).OrderBy(s => s.StartsAtUtc).ThenBy(s => s.SanctionId)
                .Select(s => LotSanctionEffects.ToDto(s, now)).ToList());
    }
}
