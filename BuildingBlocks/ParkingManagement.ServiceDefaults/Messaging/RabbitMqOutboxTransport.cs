using System.Text.Json;
using ParkingManagement.SharedKernel.Domain;

namespace ParkingManagement.ServiceDefaults.Messaging;

public sealed record IntegrationEventEnvelope(Guid EventId, string EventType, DateTime OccurredAtUtc, JsonElement Payload);

public interface IRabbitMqPublisher
{
    Task PublishAsync(OutboxMessage message, ReadOnlyMemory<byte> body, CancellationToken ct);
}

public sealed class RabbitMqOutboxTransport(IRabbitMqPublisher publisher) : IOutboxTransport
{
    public Task DeliverAsync(OutboxMessage message, CancellationToken ct)
    {
        if (message.Id == Guid.Empty || string.IsNullOrWhiteSpace(message.EventType))
            throw new ArgumentException("Sự kiện phải có EventId và EventType.");
        using var json = JsonDocument.Parse(message.PayloadJson);
        var envelope = new IntegrationEventEnvelope(message.Id, message.EventType, message.OccurredAtUtc, json.RootElement.Clone());
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return publisher.PublishAsync(message, bytes, ct);
    }
}
