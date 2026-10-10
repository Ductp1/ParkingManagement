using Microsoft.EntityFrameworkCore;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>
/// ADAPTER cài đặt IKybApplicationRepository bằng EF Core + LINQ.
/// </summary>
public sealed class KybApplicationRepository(ParkingDbContext db) : IKybApplicationRepository
{
    public Task<KybApplication?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => db.KybApplications
            .Include(k => k.ParkingLot)
            .FirstOrDefaultAsync(k => k.Id == id, cancellationToken);

    public Task<KybApplication?> GetLatestByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => db.KybApplications
            .Include(k => k.ParkingLot)
            .Where(k => k.ParkingLotId == parkingLotId)
            .OrderByDescending(k => k.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<KybApplication>> GetHistoryByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => await db.KybApplications.AsNoTracking()
            .Where(k => k.ParkingLotId == parkingLotId)
            .OrderByDescending(k => k.Id)
            .ToListAsync(cancellationToken);

    public Task AddAsync(KybApplication application, CancellationToken cancellationToken = default)
    {
        db.KybApplications.Add(application);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(KybApplication application, CancellationToken cancellationToken = default)
    {
        db.KybApplications.Update(application);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
