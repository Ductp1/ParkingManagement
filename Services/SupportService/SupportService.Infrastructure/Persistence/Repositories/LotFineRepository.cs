using Microsoft.EntityFrameworkCore;
using SupportService.Application.Interfaces;
using SupportService.Domain.Entities;

namespace SupportService.Infrastructure.Persistence.Repositories;

/// <summary>US-090: mức phạt đậu sai chỗ của từng bãi và biên bản vi phạm.</summary>
public sealed class LotFineRepository(SupportDbContext db) : ILotFineRepository
{
    public Task<LotFineRate?> FindRateAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => db.LotFineRates.FirstOrDefaultAsync(r => r.ParkingLotId == parkingLotId, cancellationToken);

    public async Task SaveRateAsync(LotFineRate rate, CancellationToken cancellationToken = default)
    {
        if (rate.Id == 0) db.LotFineRates.Add(rate);
        // 2 lần lưu mới cùng lúc cho 1 bãi → unique index ParkingLotId → 23505 → middleware trả 409.
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ParkingViolation>> ListViolationsAsync(IReadOnlyCollection<int> parkingLotIds, int take,
        CancellationToken cancellationToken = default)
        => await db.ParkingViolations.AsNoTracking().Include(v => v.Complaint)
            .Where(v => parkingLotIds.Contains(v.ParkingLotId))
            .OrderByDescending(v => v.CreatedAtUtc).Take(take).ToListAsync(cancellationToken);
}
