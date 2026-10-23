using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Repositories;

/// <summary>ADAPTER cổng ghi thao tác Admin trên KYB (US-055): hồ sơ, bãi và outbox lưu trong một SaveChanges.</summary>
public sealed class KybReviewRepository(ParkingDbContext db) : IKybReviewRepository
{
    public Task<KybApplication?> GetForReviewAsync(int id, CancellationToken cancellationToken = default)
        => db.KybApplications
            .Include(k => k.ParkingLot)
            .FirstOrDefaultAsync(k => k.Id == id, cancellationToken);

    public Task<int> CountActiveSlotsAsync(int parkingLotId, CancellationToken cancellationToken = default)
        => db.Slots.CountAsync(s => s.IsActive && s.Floor.Zone.ParkingLotId == parkingLotId, cancellationToken);

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

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Hồ sơ KYB vừa được Admin khác cập nhật, vui lòng tải lại và thử lại.");
        }
    }
}
