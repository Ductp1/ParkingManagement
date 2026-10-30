using RabbitMQ.Client;

namespace ParkingManagement.ServiceDefaults.Messaging;

public static class RabbitMqTopology
{
    public static async Task DeclareSubscriptionAsync(IChannel channel, string exchange, string queue,
        IReadOnlyList<string> routingKeys, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(queue) || queue.StartsWith("amq.", StringComparison.Ordinal)
            || routingKeys.Count == 0 || routingKeys.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Consumer cần queue riêng và ít nhất một routing key.");
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(exchange + ".dead", ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.QueueDeclareAsync(queue + ".dead", durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: ct);
        await channel.QueueBindAsync(queue + ".dead", exchange + ".dead", queue, cancellationToken: ct);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum", ["x-dead-letter-exchange"] = exchange + ".dead",
                ["x-dead-letter-routing-key"] = queue
            }, cancellationToken: ct);
        foreach (var key in routingKeys.Distinct())
            await channel.QueueBindAsync(queue, exchange, key, cancellationToken: ct);
    }
}
