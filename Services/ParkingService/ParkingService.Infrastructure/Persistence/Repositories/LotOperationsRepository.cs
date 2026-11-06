using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Features.LotOperations;
using ParkingService.Domain.Entities;
using ParkingService.Infrastructure.Persistence;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>US-068: repository heartbeat/stale cho LotOperations (Infrastructure EF implement).</summary>
public sealed class LotOperationsRepository(ParkingDbContext db) : ILotOperationsRepository
{
    public Task<ParkingLot?> GetLotAsync(int lotId, CancellationToken cancellationToken = default)
        => db.ParkingLots.FirstOrDefaultAsync(l => l.Id == lotId && !l.IsDeleted, cancellationToken);

    public Task<List<ParkingLot>> GetActiveLotsAsync(CancellationToken cancellationToken = default)
        => db.ParkingLots.Where(l => !l.IsDeleted && l.Status == ParkingLotStatus.Active).ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
