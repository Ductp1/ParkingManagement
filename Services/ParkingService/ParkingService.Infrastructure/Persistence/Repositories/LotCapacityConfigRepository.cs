using Microsoft.EntityFrameworkCore;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>
/// ADAPTER cài đặt ILotCapacityConfigRepository bằng EF Core + LINQ.
/// </summary>
public sealed class LotCapacityConfigRepository(ParkingDbContext db) : ILotCapacityConfigRepository
{
    public Task<LotCapacityConfig?> GetByParkingLotIdAsync(int parkingLotId, bool trackChanges = false, CancellationToken cancellationToken = default)
    {
        var query = trackChanges ? db.LotCapacityConfigs.AsTracking() : db.LotCapacityConfigs.AsNoTracking();
        return query.FirstOrDefaultAsync(c => c.ParkingLotId == parkingLotId, cancellationToken);
    }

    public Task AddAsync(LotCapacityConfig config, CancellationToken cancellationToken = default)
    {
        db.LotCapacityConfigs.Add(config);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(LotCapacityConfig config, CancellationToken cancellationToken = default)
    {
        db.LotCapacityConfigs.Update(config);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
