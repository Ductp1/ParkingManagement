using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>Bãi (kể cả chưa công khai, chưa soft-delete) và chủ bãi hiện tại.</summary>
public sealed record SanctionLotRow(int Id, int OwnerProfileId);

/// <summary>PORT đọc (AsNoTracking) cho hiệu lực chế tài SLA (US-097).</summary>
public interface ISanctionStateQueries
{
    Task<SanctionLotRow?> FindLotAsync(int parkingLotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chế tài đang hiệu lực tại <paramref name="nowUtc"/> áp lên các bãi: theo bãi (ParkingLotId thuộc <paramref name="parkingLotIds"/>)
    /// hoặc theo chủ bãi (ParkingLotId null, OwnerProfileId thuộc <paramref name="ownerProfileIds"/>).
    /// </summary>
    Task<IReadOnlyList<SanctionState>> ListInEffectAsync(IReadOnlyCollection<int> parkingLotIds, IReadOnlyCollection<int> ownerProfileIds,
        DateTime nowUtc, CancellationToken cancellationToken = default);
}
