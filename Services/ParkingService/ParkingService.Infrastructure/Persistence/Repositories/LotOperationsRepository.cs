using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Features.LotOperations;
using ParkingService.Domain.Entities;
using ParkingService.Infrastructure.Persistence;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>US-067/068: repository tổng quan vận hành (Infrastructure EF implement).</summary>
public sealed class LotOperationsRepository(ParkingDbContext db) : ILotOperationsRepository
{
    public Task<ParkingLot?> GetLotAsync(int lotId, CancellationToken cancellationToken = default)
        => db.ParkingLots.FirstOrDefaultAsync(l => l.Id == lotId && !l.IsDeleted, cancellationToken);

    public async Task<Dictionary<string, int>> CountSlotsByStateAsync(int lotId, CancellationToken cancellationToken = default)
    {
        // Facility → Zone → Floor → Slot: slot không có ParkingLotId trực tiếp, đi qua Zone/Floor.
        var lotFloorIds = db.Floors.Where(f => db.Zones.Any(z => z.Id == f.ZoneId && z.ParkingLotId == lotId))
            .Select(f => f.Id);
        return await db.Slots
            .Where(s => lotFloorIds.Contains(s.FloorId))
            .GroupBy(s => s.State)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key.ToString(), x => x.Count, cancellationToken);
    }

    public Task<List<ParkingLot>> GetActiveLotsAsync(CancellationToken cancellationToken = default)
        => db.ParkingLots.Where(l => !l.IsDeleted && l.Status == ParkingLotStatus.Active).ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
