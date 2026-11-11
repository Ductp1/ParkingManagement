using GateService.Application.Features.GateEvents;
using GateService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Rules;

namespace GateService.Infrastructure.Persistence;

/// <summary>Cài đặt port IGateEventWriter: ghi nhật ký sự kiện cổng xuống PM db của GateService.</summary>
public sealed class GateEventWriter(GateDbContext db) : IGateEventWriter
{
    public async Task<GateEventRecord> WriteAsync(GateEventDraft draft, CancellationToken cancellationToken = default)
    {
        var gateEvent = new GateEvent
        {
            ParkingLotId = draft.ParkingLotId,
            ParkingSessionId = draft.ParkingSessionId,
            ShiftId = draft.ShiftId,
            EventType = draft.EventType,
            PlateNumber = draft.PlateNumber,
            ImagePath = draft.ImagePath,
            PerformedByUserId = draft.PerformedByUserId,
            Note = draft.Note,
        };
        db.GateEvents.Add(gateEvent);
        await db.SaveChangesAsync(cancellationToken);

        return new GateEventRecord(gateEvent.Id, gateEvent.ParkingLotId, gateEvent.EventType.ToString(),
            gateEvent.ParkingSessionId, gateEvent.PlateNumber, gateEvent.PerformedByUserId, gateEvent.Note,
            gateEvent.ShiftId, gateEvent.CreatedAtUtc);
    }
}
