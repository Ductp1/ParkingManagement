using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>ADAPTER cài đặt ILotStalenessRepository (US-068): tracked entities + outbox cùng transaction.</summary>
public sealed class LotStalenessRepository(ParkingDbContext db) : ILotStalenessRepository
{
    public Task<ParkingLot?> GetByIdForUpdateAsync(int id, CancellationToken cancellationToken = default)
        // Tracked (không AsNoTracking) để RecordHeartbeat ghi được xuống DB.
        => db.ParkingLots.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ParkingLot>> ListActiveAsync(CancellationToken cancellationToken = default)
        // Tracked cho worker gọi MarkStale(); index (Status) phục vụ lọc. Soft-delete filter áp dụng tự động.
        => await db.ParkingLots
            .Where(p => p.Status == ParkingLotStatus.Active)
            .ToListAsync(cancellationToken);

    public async Task SaveWithEventsAsync(IReadOnlyList<IntegrationEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var ev in events)
        {
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = ev.EventId,
                EventType = ev.GetType().Name,
                OccurredAtUtc = ev.OccurredAtUtc,
                PayloadJson = JsonSerializer.Serialize(ev, ev.GetType()),
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
