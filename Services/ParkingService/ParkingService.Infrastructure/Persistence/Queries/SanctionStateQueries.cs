using Microsoft.EntityFrameworkCore;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Queries;

/// <summary>US-097: đọc hiệu lực chế tài (AsNoTracking). Hiệu lực lọc ở PostgreSQL bằng cùng biểu thức với Domain.</summary>
public sealed class SanctionStateQueries(ParkingDbContext db) : ISanctionStateQueries
{
    public Task<SanctionLotRow?> FindLotAsync(int parkingLotId, CancellationToken cancellationToken = default)
        // Bỏ qua trạng thái bãi (kể cả PendingApproval / Suspended); soft-delete đã lọc bằng query filter.
        => db.ParkingLots.AsNoTracking()
            .Where(p => p.Id == parkingLotId)
            .Select(p => new SanctionLotRow(p.Id, p.OwnerProfileId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<SanctionState>> ListInEffectAsync(IReadOnlyCollection<int> parkingLotIds,
        IReadOnlyCollection<int> ownerProfileIds, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        if (parkingLotIds.Count == 0 && ownerProfileIds.Count == 0) return [];

        return await db.Set<SanctionState>().AsNoTracking()
            .Where(SanctionState.InEffectAt(nowUtc))
            .Where(s => (s.ParkingLotId != null && parkingLotIds.Contains(s.ParkingLotId.Value))
                        || (s.ParkingLotId == null && ownerProfileIds.Contains(s.OwnerProfileId)))
            .ToListAsync(cancellationToken);
    }
}
