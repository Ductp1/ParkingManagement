using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Application.Features.LotOperations;
using ParkingService.Infrastructure.Persistence;

namespace ParkingService.Infrastructure.Integrations;

/// <summary>US-068: publisher outbox — event ghi OutboxMessages của ParkingDbContext (cùng transaction).</summary>
public sealed class ParkingEventOutbox(ParkingDbContext db) : ILotEventPublisher
{
    public void Enqueue(IntegrationEvent integrationEvent)
        => db.OutboxMessages.Add(new ParkingManagement.SharedKernel.Domain.OutboxMessage
        {
            EventType = integrationEvent.GetType().Name,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
            OccurredAtUtc = DateTime.UtcNow,
        });
}
