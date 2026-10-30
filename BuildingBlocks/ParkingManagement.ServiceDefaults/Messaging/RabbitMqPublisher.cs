using MassTransit;
using Microsoft.Extensions.Options;

namespace ParkingManagement.ServiceDefaults.Messaging;

// MassTransit owns connection/channel lifecycle; the database Outbox owns delivery retry.
public sealed class RabbitMqPublisher(IBus bus, IOptions<RabbitMqOptions> options) : IRabbitMqPublisher
{
    public async Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.Value.OperationTimeoutSeconds, 1, 60)));
        var endpoint = await bus.GetSendEndpoint(new Uri($"exchange:{options.Value.Exchange}?type=topic"));
        await endpoint.Send(envelope, context =>
        {
            context.MessageId = envelope.EventId;
            context.Durable = true;
            context.SetRoutingKey(envelope.EventType);
            var rabbit = context.GetPayload<RabbitMqSendContext>();
            rabbit.Mandatory = true;
            rabbit.AwaitAck = true;
        }, timeout.Token);
    }
}
