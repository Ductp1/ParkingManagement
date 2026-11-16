using System.Text.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingService.Application.Features.LotOperations;
using ParkingService.Infrastructure.Persistence;

namespace ParkingService.Infrastructure.Integrations;

/// <summary>
/// US-069: ILotEventPublisher production — ghi IntegrationEvent vào bảng OutboxMessages của
/// ParkingDbContext (cùng scope với LotOperationsRepository). Use case gọi Enqueue(...) rồi
/// repository.SaveChangesAsync → lot + audit log + outbox PERSIST ATOMIC trong 1 transaction.
/// OutboxDispatcher&lt;ParkingDbContext&gt; (ServiceDefaults) sau đó đẩy RabbitMQ at-least-once;
/// NotificationService chống trùng bằng EventId (TransactionalInbox).
/// </summary>
public sealed class OutboxLotEventPublisher(ParkingDbContext db) : ILotEventPublisher
{
    public void Enqueue(IntegrationEvent integrationEvent)
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = integrationEvent.EventId,
            EventType = integrationEvent.GetType().Name,
            OccurredAtUtc = integrationEvent.OccurredAtUtc,
            PayloadJson = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
        });
    }
}