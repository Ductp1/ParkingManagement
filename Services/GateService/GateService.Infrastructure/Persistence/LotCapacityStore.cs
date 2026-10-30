using GateService.Application.Features.LotCapacity;
using GateService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GateService.Infrastructure.Persistence;

/// <summary>Store cấu hình sức chứa + counters + audit log (US-066) trên GateDbContext.</summary>
public sealed class LotCapacityStore(GateDbContext db) : ILotCapacityPolicyStore
{
    public Task<LotCapacityConfig?> GetAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => db.LotCapacityConfigs.AsNoTracking()
            .SingleOrDefaultAsync(c => c.ParkingLotId == parkingLotId, cancellationToken);

    public async Task<AdmissionCounters?> GetCountersAsync(int parkingLotId, CancellationToken cancellationToken = default)
    {
        var counter = await db.LotAdmissionCounters.AsNoTracking()
            .SingleOrDefaultAsync(c => c.ParkingLotId == parkingLotId, cancellationToken);
        return counter is null ? null : new AdmissionCounters(counter.Total, counter.Transient, counter.Subscriber);
    }

    public async Task AddAsync(LotCapacityConfig config, CancellationToken cancellationToken = default)
        => await db.LotCapacityConfigs.AddAsync(config, cancellationToken);

    public async Task AddAuditAsync(LotCapacityAuditLog log, CancellationToken cancellationToken = default)
        => await db.LotCapacityAuditLogs.AddAsync(log, cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}