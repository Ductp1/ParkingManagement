using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Features.LotOperations;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>
/// ADAPTER US-067/068/069: Lot Operations trên ParkingDbContext.
/// EmergencyStopLog + outbox (qua OutboxLotEventPublisher cùng scope) flush 1 lần trong SaveChangesAsync.
/// </summary>
public sealed class LotOperationsRepository(ParkingDbContext db) : ILotOperationsRepository
{
    public Task<ParkingLot?> GetLotAsync(int lotId, CancellationToken cancellationToken = default)
        => db.ParkingLots.FirstOrDefaultAsync(l => l.Id == lotId, cancellationToken);

    public async Task<Dictionary<string, int>> CountSlotsByStateAsync(int lotId, CancellationToken cancellationToken = default)
        => await db.Slots
            .Where(s => s.IsActive && s.Floor.Zone.ParkingLotId == lotId)
            .GroupBy(s => s.State)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key.ToString(), g => g.Count, cancellationToken);

    public async Task AddEmergencyLogAsync(EmergencyStopLog log, CancellationToken cancellationToken = default)
    {
        db.EmergencyStopLogs.Add(log);
        await Task.CompletedTask;
    }

    public async Task<List<EmergencyStopLog>> GetEmergencyLogsAsync(int lotId, int take, CancellationToken cancellationToken = default)
        => await db.EmergencyStopLogs
            .Where(l => l.ParkingLotId == lotId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<List<ParkingLot>> GetActiveLotsAsync(CancellationToken cancellationToken = default)
        => await db.ParkingLots
            .Where(l => l.Status == ParkingLotStatus.Active)
            .OrderBy(l => l.Id)
            .ToListAsync(cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await db.SaveChangesAsync(cancellationToken);
}