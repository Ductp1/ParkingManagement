using Microsoft.EntityFrameworkCore;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>
/// ADAPTER cài đặt ILayoutVersionRepository bằng EF Core + LINQ.
/// </summary>
public sealed class LayoutVersionRepository(ParkingDbContext db) : ILayoutVersionRepository
{
    public Task<LayoutVersion?> GetLatestPublishedByFloorIdAsync(int floorId, CancellationToken cancellationToken = default)
        => db.LayoutVersions.AsNoTracking()
            .Where(lv => lv.FloorId == floorId && lv.IsPublished)
            .OrderByDescending(lv => lv.VersionNo)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<LayoutVersion?> GetByFloorIdAndVersionAsync(int floorId, int versionNo, CancellationToken cancellationToken = default)
        => db.LayoutVersions.AsNoTracking()
            .FirstOrDefaultAsync(lv => lv.FloorId == floorId && lv.VersionNo == versionNo, cancellationToken);

    public async Task<IReadOnlyList<LayoutVersion>> GetAllVersionsByFloorIdAsync(int floorId, CancellationToken cancellationToken = default)
        => await db.LayoutVersions.AsNoTracking()
            .Where(lv => lv.FloorId == floorId)
            .OrderByDescending(lv => lv.VersionNo)
            .ToListAsync(cancellationToken);

    public async Task<int> GetNextVersionNoAsync(int floorId, CancellationToken cancellationToken = default)
    {
        var maxVersion = await db.LayoutVersions
            .Where(lv => lv.FloorId == floorId)
            .Select(lv => (int?)lv.VersionNo)
            .MaxAsync(cancellationToken);

        return (maxVersion ?? 0) + 1;
    }

    public Task AddAsync(LayoutVersion layoutVersion, CancellationToken cancellationToken = default)
    {
        db.LayoutVersions.Add(layoutVersion);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(LayoutVersion layoutVersion, CancellationToken cancellationToken = default)
    {
        db.LayoutVersions.Update(layoutVersion);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
