using GateService.Application.Features.GateEvents;
using GateService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GateService.Infrastructure.Persistence;

/// <summary>Cài đặt port IGateEventWriter: ghi nhật ký sự kiện cổng xuống PM db của GateService.</summary>
public sealed class GateEventWriter(GateDbContext db) : IGateEventWriter
{
    public async Task WriteAsync(GateEventDraft draft, CancellationToken cancellationToken = default)
    {
        db.GateEvents.Add(new GateEvent
        {
            ParkingLotId = draft.ParkingLotId,
            ParkingSessionId = draft.ParkingSessionId,
            EventType = draft.EventType,
            PlateNumber = draft.PlateNumber,
            PerformedByUserId = draft.PerformedByUserId,
            Note = draft.Note,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
