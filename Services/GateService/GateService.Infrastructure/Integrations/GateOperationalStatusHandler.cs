using System.Text.Json;
using GateService.Domain.Entities;
using GateService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.SharedKernel.Contracts;

namespace GateService.Infrastructure.Integrations;

/// <summary>
/// US-069: consumer GateService — cache trạng thái emergency stop vào LotOperationalStatus
/// để LotAdmissionGate chặn check-in mới khi bãi đóng. Idempotency qua TransactionalInbox (EventId).
/// </summary>
public sealed class GateOperationalStatusHandler(GateDbContext db, ILogger<GateOperationalStatusHandler> logger) : IIntegrationEventHandler
{
    public async Task HandleAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        switch (envelope.EventType)
        {
            case nameof(LotEmergencyStopped):
            {
                var evt = JsonSerializer.Deserialize<LotEmergencyStopped>(envelope.Payload.GetRawText())
                          ?? throw new JsonException("Payload LotEmergencyStopped rỗng.");
                var status = await db.LotOperationalStatuses.SingleOrDefaultAsync(s => s.ParkingLotId == evt.ParkingLotId, cancellationToken);
                if (status is null)
                {
                    status = new LotOperationalStatus { ParkingLotId = evt.ParkingLotId };
                    db.LotOperationalStatuses.Add(status);
                }
                status.IsClosed = true;
                status.ClosedReason = evt.Reason;
                status.ClosedAtUtc = evt.StoppedAtUtc;
                await db.SaveChangesAsync(cancellationToken);
                logger.LogWarning("Bãi {LotId} ĐÓNG khẩn cấp — chặn check-in mới. Lý do: {Reason}", evt.ParkingLotId, evt.Reason);
                break;
            }
            case nameof(LotEmergencyLifted):
            {
                var evt = JsonSerializer.Deserialize<LotEmergencyLifted>(envelope.Payload.GetRawText())
                          ?? throw new JsonException("Payload LotEmergencyLifted rỗng.");
                var status = await db.LotOperationalStatuses.SingleOrDefaultAsync(s => s.ParkingLotId == evt.ParkingLotId, cancellationToken);
                if (status is not null)
                {
                    status.IsClosed = false;
                    status.ClosedReason = null;
                    status.ClosedAtUtc = null;
                    await db.SaveChangesAsync(cancellationToken);
                }
                logger.LogInformation("Bãi {LotId} đã MỞ lại sau emergency stop.", evt.ParkingLotId);
                break;
            }
            default:
                logger.LogInformation("Event {EventType} không thuộc phạm vi GateService — bỏ qua.", envelope.EventType);
                break;
        }
    }
}
