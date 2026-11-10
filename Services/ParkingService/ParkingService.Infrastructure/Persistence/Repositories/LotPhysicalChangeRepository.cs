using Microsoft.EntityFrameworkCore;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>
/// ADAPTER cài đặt ILotPhysicalChangeRepository bằng EF Core + LINQ trên DbContext của ParkingService.
/// </summary>
public sealed class LotPhysicalChangeRepository(ParkingDbContext db) : ILotPhysicalChangeRepository
{
    public Task<LotPhysicalChange?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => db.LotPhysicalChanges
            .Include(c => c.ParkingLot)
            .Include(c => c.RelatedLayoutVersion)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<LotPhysicalChange>> GetByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => await db.LotPhysicalChanges.AsNoTracking()
            .Where(c => c.ParkingLotId == parkingLotId)
            .OrderByDescending(c => c.OccurredAtUtc)
            .ThenByDescending(c => c.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LotPhysicalChange>> GetActiveByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => await db.LotPhysicalChanges
            .Where(c => c.ParkingLotId == parkingLotId &&
                        (c.Status == PhysicalChangeStatus.InProgress || c.Status == PhysicalChangeStatus.Overdue))
            .OrderByDescending(c => c.OccurredAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LotPhysicalChange>> GetOverdueCandidatesAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
        => await db.LotPhysicalChanges
            .Where(c => c.Status == PhysicalChangeStatus.InProgress && c.DeadlineUtc < nowUtc)
            .ToListAsync(cancellationToken);

    public Task AddAsync(LotPhysicalChange change, CancellationToken cancellationToken = default)
    {
        db.LotPhysicalChanges.Add(change);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(LotPhysicalChange change, CancellationToken cancellationToken = default)
    {
        db.LotPhysicalChanges.Update(change);
        return Task.CompletedTask;
    }

    public Task<ParkingLot?> GetLotByIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => db.ParkingLots.FirstOrDefaultAsync(p => p.Id == parkingLotId && !p.IsDeleted, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
