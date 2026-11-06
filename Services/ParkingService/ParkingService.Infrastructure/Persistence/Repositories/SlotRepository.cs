using Microsoft.EntityFrameworkCore;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>
/// ADAPTER cài đặt ISlotRepository bằng EF Core + LINQ.
/// </summary>
public sealed class SlotRepository(ParkingDbContext db) : ISlotRepository
{
    public Task<Slot?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => db.Slots.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Slot>> GetByFloorIdAsync(int floorId, CancellationToken cancellationToken = default)
        => await db.Slots.AsNoTracking()
            .Where(s => s.FloorId == floorId)
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsCodeAsync(int floorId, string code, int? excludeId = null, CancellationToken cancellationToken = default)
        => db.Slots.AnyAsync(
            s => s.FloorId == floorId
                 && s.Code == code
                 && (!excludeId.HasValue || s.Id != excludeId.Value),
            cancellationToken);

    public Task<bool> IsGridCellOccupiedAsync(int floorId, int gridX, int gridY, int? excludeId = null, CancellationToken cancellationToken = default)
        => db.Slots.AnyAsync(
            s => s.FloorId == floorId
                 && s.GridX == gridX
                 && s.GridY == gridY
                 && (!excludeId.HasValue || s.Id != excludeId.Value),
            cancellationToken);

    public Task AddAsync(Slot slot, CancellationToken cancellationToken = default)
    {
        db.Slots.Add(slot);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IEnumerable<Slot> slots, CancellationToken cancellationToken = default)
    {
        db.Slots.AddRange(slots);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Slot slot, CancellationToken cancellationToken = default)
    {
        db.Slots.Update(slot);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Slot slot, CancellationToken cancellationToken = default)
    {
        db.Slots.Remove(slot);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
