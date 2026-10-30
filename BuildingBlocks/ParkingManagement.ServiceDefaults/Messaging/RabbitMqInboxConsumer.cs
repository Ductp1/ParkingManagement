using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Options;
using ParkingManagement.ServiceDefaults.Persistence;

namespace ParkingManagement.ServiceDefaults.Messaging;

// Handler must use the same scoped DbContext and limit effects to that database transaction.
public interface IIntegrationEventHandler
{
    Task HandleAsync(IntegrationEventEnvelope integrationEvent, CancellationToken ct);
}

public sealed class RabbitMqInboxConsumer<TContext, THandler>(TContext db, THandler handler,
    IOptions<RabbitMqSubscription> subscription) : IConsumer<IntegrationEventEnvelope>
    where TContext : ServiceDbContext where THandler : class, IIntegrationEventHandler
{
    public async Task Consume(ConsumeContext<IntegrationEventEnvelope> context)
    {
        var envelope = context.Message;
        var routingKey = context.GetPayload<RabbitMqBasicConsumeContext>().RoutingKey;
        if (envelope.EventId == Guid.Empty || context.MessageId != envelope.EventId
            || string.IsNullOrWhiteSpace(envelope.EventType) || envelope.EventType != routingKey
            || !subscription.Value.RoutingKeys.Contains(envelope.EventType)
            || envelope.Payload.ValueKind != JsonValueKind.Object)
            throw new JsonException("Envelope, routing key hoặc MessageId không hợp lệ.");
        // MassTransit acks only after this transaction completes; duplicate redelivery is harmless.
        await db.ApplyOnceAsync(envelope.EventId,
            ct => handler.HandleAsync(envelope, ct), context.CancellationToken);
    }
}
