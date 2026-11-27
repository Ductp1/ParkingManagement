using Microsoft.EntityFrameworkCore;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>
/// ADAPTER cài đặt IFloorRepository bằng EF Core + LINQ.
/// </summary>
public sealed class FloorRepository(ParkingDbContext db) : IFloorRepository
{
    public Task<Floor?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => db.Floors.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<Floor?> GetWithSlotsAsync(int id, CancellationToken cancellationToken = default)
        => db.Floors.AsNoTracking()
            .Include(f => f.Slots.OrderBy(s => s.Code))
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Floor>> GetByZoneIdAsync(int zoneId, CancellationToken cancellationToken = default)
        => await db.Floors.AsNoTracking()
            .Where(f => f.ZoneId == zoneId)
            .OrderBy(f => f.Level)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsNameAsync(int zoneId, string name, int? excludeId = null, CancellationToken cancellationToken = default)
        => db.Floors.AnyAsync(
            f => f.ZoneId == zoneId
                 && f.Name == name
                 && (!excludeId.HasValue || f.Id != excludeId.Value),
            cancellationToken);

    public Task AddAsync(Floor floor, CancellationToken cancellationToken = default)
    {
        db.Floors.Add(floor);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Floor floor, CancellationToken cancellationToken = default)
    {
        db.Floors.Update(floor);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Floor floor, CancellationToken cancellationToken = default)
    {
        db.Floors.Remove(floor);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
