using GateService.Application.Features;
using GateService.Application.Features.GateEvents;
using GateService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Rules;

namespace GateService.Infrastructure.Persistence.Queries;

/// <summary>Đọc GateEvents cho audit ngoại lệ (US-048) và thống kê hủy No-show của Staff (US-077).</summary>
public sealed class GateEventQueries(GateDbContext db) : IGateEventQueries
{
    public async Task<IReadOnlyList<GateEventDto>> ListAsync(int parkingLotId, GateEventType? eventType,
        DateTime? fromUtc, DateTime? toUtc, int limit, CancellationToken cancellationToken)
    {
        var query = db.GateEvents.AsNoTracking().Where(e => e.ParkingLotId == parkingLotId);
        if (eventType is { } type) query = query.Where(e => e.EventType == type);
        if (fromUtc is { } from) query = query.Where(e => e.CreatedAtUtc >= from);
        if (toUtc is { } to) query = query.Where(e => e.CreatedAtUtc <= to);

        var rows = await query
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);

        return rows.Select(e => new GateEventDto(e.Id, e.ParkingLotId, e.EventType.ToString(), e.ParkingSessionId,
            e.PlateNumber, e.PlateNumber is null ? null : PlateNormalizer.Format(e.PlateNumber),
            e.PerformedByUserId, e.Note, e.CreatedAtUtc)).ToList();
    }

    public async Task<IReadOnlyList<StaffNoShowCancelStat>> NoShowCancellationStatsAsync(DateTime fromUtc, DateTime toUtc,
        int? parkingLotId, CancellationToken cancellationToken)
    {
        var query = db.GateEvents.AsNoTracking()
            .Where(e => e.EventType == GateEventType.NoShowCancel
                        && e.CreatedAtUtc >= fromUtc && e.CreatedAtUtc <= toUtc);
        if (parkingLotId is { } lotId) query = query.Where(e => e.ParkingLotId == lotId);

        var rows = await query
            .GroupBy(e => new { e.ParkingLotId, e.PerformedByUserId })
            .Select(g => new
            {
                g.Key.ParkingLotId,
                g.Key.PerformedByUserId,
                CancelCount = g.Count(),
                LastCancelAtUtc = g.Max(e => e.CreatedAtUtc)
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new StaffNoShowCancelStat(r.ParkingLotId, r.PerformedByUserId, r.CancelCount, r.LastCancelAtUtc))
            .ToList();
    }
}