using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ParkingManagement.ServiceDefaults.Messaging;

// One long-lived connection per service, separate channels for publisher and consumer.
public sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options, IHostEnvironment environment) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IConnection? connection;
    public async Task<IConnection> GetAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (connection is { IsOpen: true }) return connection;
            if (connection is not null) await connection.DisposeAsync();
            connection = await options.Value.CreateFactory(environment).CreateConnectionAsync(ct);
            return connection;
        }
        finally { gate.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        if (connection is not null) await connection.DisposeAsync();
        gate.Dispose();
    }
}
