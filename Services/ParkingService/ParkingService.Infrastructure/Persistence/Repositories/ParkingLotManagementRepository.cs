using Microsoft.EntityFrameworkCore;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>
/// ADAPTER cài đặt IParkingLotManagementRepository bằng EF Core + LINQ trên DbContext của ParkingService.
/// </summary>
public sealed class ParkingLotManagementRepository(ParkingDbContext db) : IParkingLotManagementRepository
{
    public Task<ParkingLot?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => db.ParkingLots.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

    public Task<ParkingLot?> GetHierarchyAsync(int id, CancellationToken cancellationToken = default)
        => db.ParkingLots.AsNoTracking()
            .Include(p => p.Zones.OrderBy(z => z.SortOrder))
                .ThenInclude(z => z.Floors.OrderBy(f => f.Level))
                    .ThenInclude(f => f.Slots.OrderBy(s => s.Code))
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<ParkingLot>> GetByOwnerProfileIdAsync(int ownerProfileId, CancellationToken cancellationToken = default)
        => await db.ParkingLots.AsNoTracking()
            .Where(p => p.OwnerProfileId == ownerProfileId && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsByNameAsync(int ownerProfileId, string name, int? excludeId = null, CancellationToken cancellationToken = default)
        => db.ParkingLots.AnyAsync(
            p => p.OwnerProfileId == ownerProfileId
                 && p.Name == name
                 && (!excludeId.HasValue || p.Id != excludeId.Value)
                 && !p.IsDeleted,
            cancellationToken);

    public Task AddAsync(ParkingLot lot, CancellationToken cancellationToken = default)
    {
        db.ParkingLots.Add(lot);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ParkingLot lot, CancellationToken cancellationToken = default)
    {
        db.ParkingLots.Update(lot);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
