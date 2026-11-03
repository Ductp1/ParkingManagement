using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.Interfaces;
using SupportService.Domain.Entities;
using SupportService.Domain.Rules;

namespace SupportService.Infrastructure.Persistence.Repositories;

public sealed class LotFullIncidentRepository(SupportDbContext db) : ILotFullIncidentRepository
{
    public Task<bool> ExistsForBookingAsync(int bookingId, CancellationToken cancellationToken = default)
        => db.LotFullIncidents.AnyAsync(i => i.BookingId == bookingId, cancellationToken);

    public Task<LotFullIncident?> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
        => db.LotFullIncidents.FirstOrDefaultAsync(i => i.Code == code, cancellationToken);

    public async Task AddAsync(LotFullIncident incident, IReadOnlyCollection<IntegrationEvent> events, CancellationToken cancellationToken = default)
    {
        db.LotFullIncidents.Add(incident);
        await SaveWithOutboxAsync(incident, events, cancellationToken);
    }

    public Task UpdateAsync(LotFullIncident incident, IReadOnlyCollection<IntegrationEvent> events, CancellationToken cancellationToken = default)
        => SaveWithOutboxAsync(incident, events, cancellationToken);

    public async Task<IReadOnlyList<LotFullIncident>> ListAsync(int? parkingLotId, LotFullIncidentStatus? status, bool slaViolationOnly,
        int take, CancellationToken cancellationToken = default)
    {
        var query = db.LotFullIncidents.AsNoTracking();
        if (parkingLotId is { } lot) query = query.Where(i => i.ParkingLotId == lot);
        if (status is { } s) query = query.Where(i => i.Status == s);
        if (slaViolationOnly) query = query.Where(i => i.IsSlaViolation);
        return await query.OrderByDescending(i => i.CreatedAtUtc).Take(take).ToListAsync(cancellationToken);
    }

    /// <summary>Transactional Outbox: sự cố và sự kiện (thông báo, chế tài SLA) cùng thành công hoặc cùng thất bại.</summary>
    private async Task SaveWithOutboxAsync(LotFullIncident incident, IReadOnlyCollection<IntegrationEvent> events, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var evt in events)
        {
            db.OutboxMessages.Add(new OutboxMessage
            {
                EventType = evt.GetType().Name,
                PayloadJson = JsonSerializer.Serialize(evt, evt.GetType()),
                OccurredAtUtc = evt.OccurredAtUtc,
            });
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw new ConflictException($"Sự cố {incident.Code} vừa được xử lý bởi thao tác khác. Vui lòng tải lại.");
        }
        await transaction.CommitAsync(cancellationToken);
    }
}
