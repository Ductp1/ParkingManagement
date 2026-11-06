using Microsoft.EntityFrameworkCore;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>
/// ADAPTER cài đặt IZoneRepository bằng EF Core + LINQ.
/// </summary>
public sealed class ZoneRepository(ParkingDbContext db) : IZoneRepository
{
    public Task<Zone?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => db.Zones.FirstOrDefaultAsync(z => z.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Zone>> GetByParkingLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => await db.Zones.AsNoTracking()
            .Where(z => z.ParkingLotId == parkingLotId)
            .OrderBy(z => z.SortOrder)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsCodeAsync(int parkingLotId, string code, int? excludeId = null, CancellationToken cancellationToken = default)
        => db.Zones.AnyAsync(
            z => z.ParkingLotId == parkingLotId
                 && z.Code == code
                 && (!excludeId.HasValue || z.Id != excludeId.Value),
            cancellationToken);

    public Task AddAsync(Zone zone, CancellationToken cancellationToken = default)
    {
        db.Zones.Add(zone);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Zone zone, CancellationToken cancellationToken = default)
    {
        db.Zones.Update(zone);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Zone zone, CancellationToken cancellationToken = default)
    {
        db.Zones.Remove(zone);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
