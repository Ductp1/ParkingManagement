using Microsoft.Extensions.Options;
using ParkingManagement.SharedKernel.Domain;
using RabbitMQ.Client;

namespace ParkingManagement.ServiceDefaults.Messaging;

public sealed class RabbitMqPublisher(RabbitMqConnection connections, IOptions<RabbitMqOptions> options) : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IChannel? channel;
    public async Task PublishAsync(OutboxMessage message, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.Value.OperationTimeoutSeconds, 1, 60)));
        var token = timeout.Token;
        await gate.WaitAsync(token); // IChannel must not publish concurrently.
        try
        {
            if (channel is null || !channel.IsOpen)
            {
                await ResetChannelAsync();
                var connection = await connections.GetAsync(token);
                channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true), token);
                await channel.ExchangeDeclareAsync(options.Value.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: token);
            }
            var properties = new BasicProperties
            {
                MessageId = message.Id.ToString(), Type = message.EventType, ContentType = "application/json",
                ContentEncoding = "utf-8", Persistent = true,
                Timestamp = new AmqpTimestamp(new DateTimeOffset(message.OccurredAtUtc).ToUnixTimeSeconds())
            };
            // With tracking enabled this awaits confirmation and throws on nack/basic.return.
            // No queue bound to the routing key is an error, not a successful dispatch.
            await channel.BasicPublishAsync(options.Value.Exchange, message.EventType, mandatory: true,
                basicProperties: properties, body: body, cancellationToken: token);
        }
        catch { await ResetChannelAsync(); throw; }
        finally { gate.Release(); }
    }
    private async Task ResetChannelAsync()
    {
        var previous = channel; channel = null;
        if (previous is not null)
        {
            try { await previous.DisposeAsync(); }
            catch { /* Cleanup must not mask a publish failure; Outbox will retry. */ }
        }
    }
    public async ValueTask DisposeAsync() { await ResetChannelAsync(); gate.Dispose(); }
}
