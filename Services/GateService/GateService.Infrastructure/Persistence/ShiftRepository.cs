using System.Linq.Expressions;
using System.Text.Json;
using GateService.Application.Features.GateExceptions;
using GateService.Application.Features.Shifts;
using GateService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Infrastructure.Persistence;

/// <summary>Port IShiftRepository trên GateDbContext: ca, mẫu ca, số liệu tổng hợp và outbox lưu trong một SaveChanges.</summary>
public sealed class ShiftRepository(GateDbContext db) : IShiftRepository
{
    public Task<Shift?> FindAsync(int id, CancellationToken ct) => db.Shifts.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<List<Shift>> WhereAsync(Expression<Func<Shift, bool>> predicate, CancellationToken ct)
        => db.Shifts.Where(predicate).ToListAsync(ct);

    public async Task<(IReadOnlyList<Shift> Items, int Total)> PageAsync(Expression<Func<Shift, bool>> predicate, int page, int pageSize,
        CancellationToken ct)
    {
        var query = db.Shifts.AsNoTracking().Where(predicate);
        var items = await query.OrderByDescending(s => s.ScheduledStartUtc).ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, await query.CountAsync(ct));
    }

    public void Add(Shift shift) => db.Shifts.Add(shift);
    public void Remove(Shift shift) => db.Shifts.Remove(shift);

    public Task<List<ShiftTemplate>> TemplatesAsync(int parkingLotId, CancellationToken ct)
        => db.ShiftTemplates.Where(t => t.ParkingLotId == parkingLotId).OrderBy(t => t.StartTime).ThenBy(t => t.Id).ToListAsync(ct);

    public Task<ShiftTemplate?> FindTemplateAsync(int id, CancellationToken ct) => db.ShiftTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
    public void Add(ShiftTemplate template) => db.ShiftTemplates.Add(template);
    public void Remove(ShiftTemplate template) => db.ShiftTemplates.Remove(template);

    public async Task<ShiftEventCounts> CountEventsAsync(int shiftId, CancellationToken ct)
    {
        var counts = await db.GateEvents.Where(e => e.ShiftId == shiftId).GroupBy(e => e.EventType)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        int Of(GateEventType type) => counts.GetValueOrDefault(type);
        return new ShiftEventCounts(Of(GateEventType.CheckIn), Of(GateEventType.CheckOut), GateExceptionTypes.Allowed.Sum(Of),
            Of(GateEventType.EmergencyBarrierOpen), Of(GateEventType.NoShowCancel));
    }

    public Task<int> CountVehiclesInLotAsync(int parkingLotId, CancellationToken ct)
        => db.ParkingSessions.CountAsync(s => s.ParkingLotId == parkingLotId && s.Status == ParkingSessionStatus.Active, ct);

    public Task<int> CountPendingPmsAsync(int parkingLotId, CancellationToken ct)
        => db.PmsUnmatchedEvents.CountAsync(e => e.ParkingLotId == parkingLotId && e.Status == PmsQuarantineStatus.Pending, ct);

    public void Enqueue(IntegrationEvent integrationEvent) => db.OutboxMessages.Add(new OutboxMessage
    {
        Id = integrationEvent.EventId,
        EventType = integrationEvent.GetType().Name,
        OccurredAtUtc = integrationEvent.OccurredAtUtc,
        PayloadJson = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
    });

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
