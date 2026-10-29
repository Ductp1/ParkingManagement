using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.SharedKernel.Domain;
using RabbitMQ.Client;
using UserService.Infrastructure.Persistence;

namespace UserService.Test;

public sealed class RabbitMqFactAttribute : FactAttribute
{
    public RabbitMqFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TV1_TEST_RABBITMQ")))
            Skip = "Cần TV1_TEST_RABBITMQ trỏ broker test/local. Xem scripts/test-rabbitmq.ps1.";
    }
}
public sealed class RabbitMqPostgresFactAttribute : FactAttribute
{
    public RabbitMqPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TV1_TEST_RABBITMQ"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TV1_TEST_DB")))
            Skip = "Cần TV1_TEST_RABBITMQ và TV1_TEST_DB để test consumer/Inbox thật.";
    }
}

public sealed class RabbitMqBrokerTests
{
    [RabbitMqFact]
    public async Task Confirmed_persistent_event_reaches_independent_queues_with_stable_id()
    {
        await using var fixture = await Broker.CreateAsync();
        await fixture.SubscribeAsync("a"); await fixture.SubscribeAsync("b");
        var message = new OutboxMessage { EventType = "UserRegistered", PayloadJson = "{\"UserId\":7}" };
        await fixture.Transport.DeliverAsync(message, default);
        await fixture.Transport.DeliverAsync(message, default); // A retry may duplicate delivery; Inbox deduplicates effects.
        foreach (var suffix in new[] { "a", "b" })
            for (var i = 0; i < 2; i++)
            {
                var received = await fixture.GetAsync(fixture.Queue(suffix));
                Assert.Equal(message.Id.ToString(), received.BasicProperties.MessageId);
                Assert.Equal("UserRegistered", received.RoutingKey);
                Assert.Equal("application/json", received.BasicProperties.ContentType);
                Assert.True(received.BasicProperties.Persistent);
                using var json = JsonDocument.Parse(received.Body);
                Assert.Equal(message.Id, json.RootElement.GetProperty("eventId").GetGuid());
                Assert.Equal(7, json.RootElement.GetProperty("payload").GetProperty("UserId").GetInt32());
            }
    }

    [RabbitMqFact]
    public async Task Mandatory_unroutable_publish_fails_instead_of_marking_delivery_successful()
    {
        await using var fixture = await Broker.CreateAsync();
        await Assert.ThrowsAsync<RabbitMQ.Client.Exceptions.PublishReturnException>(() => fixture.Transport.DeliverAsync(
            new OutboxMessage { EventType = "UserRegistered", PayloadJson = "{}" }, default));
        // The same connection/publisher recovers after a return; no process restart required.
        await fixture.SubscribeAsync("recovered");
        var message = new OutboxMessage { EventType = "UserRegistered", PayloadJson = "{}" };
        await fixture.Transport.DeliverAsync(message, default);
        Assert.Equal(message.Id.ToString(), (await fixture.GetAsync(fixture.Queue("recovered"))).BasicProperties.MessageId);
    }

    [RabbitMqPostgresFact]
    public async Task Consumer_commits_each_event_once_and_dead_letters_failed_transaction()
    {
        await using var fixture = await Broker.CreateAsync();
        await fixture.SubscribeAsync("inbox");
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("TV1_TEST_DB"))
            { Database = "pm_tv1_rabbit_" + Guid.NewGuid().ToString("N") };
        using var services = new ServiceCollection().AddDbContext<UserDbContext>(o => o.UseNpgsql(builder.ConnectionString))
            .AddScoped<RecordingHandler>().BuildServiceProvider();
        UserDbContext Context() => new(new DbContextOptionsBuilder<UserDbContext>().UseNpgsql(builder.ConnectionString).Options);
        await using (var db = Context()) await db.Database.EnsureCreatedAsync();
        try
        {
            using var consumer = new RabbitMqInboxConsumer<UserDbContext, RecordingHandler>(fixture.Connections,
                Options.Create(fixture.Settings), Options.Create(new RabbitMqSubscription { Queue = fixture.Queue("inbox"), RoutingKeys = ["UserRegistered"] }),
                services.GetRequiredService<IServiceScopeFactory>(), NullLogger<RabbitMqInboxConsumer<UserDbContext, RecordingHandler>>.Instance);
            await consumer.StartAsync(default);
            try
            {
                var first = new OutboxMessage { EventType = "UserRegistered", PayloadJson = "{}" };
                var failed = new OutboxMessage { EventType = "UserRegistered", PayloadJson = "{\"fail\":true}" };
                var barrier = new OutboxMessage { EventType = "UserRegistered", PayloadJson = "{}" };
                await fixture.Transport.DeliverAsync(first, default);
                await fixture.Transport.DeliverAsync(first, default);
                await fixture.Transport.DeliverAsync(failed, default);
                await fixture.Transport.DeliverAsync(barrier, default);
                using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                while (true)
                {
                    await using var db = Context();
                    if (await db.Set<InboxReceipt>().AnyAsync(r => r.EventId == barrier.Id, wait.Token)) break;
                    await Task.Delay(100, wait.Token);
                }
                await using (var db = Context())
                {
                    Assert.Equal(2, await db.Set<InboxReceipt>().CountAsync());
                    Assert.Equal(2, await db.OutboxMessages.CountAsync());
                    Assert.False(await db.Set<InboxReceipt>().AnyAsync(r => r.EventId == failed.Id));
                }
                var dead = await fixture.GetAsync(fixture.Queue("inbox") + ".dead");
                Assert.Equal(failed.Id.ToString(), dead.BasicProperties.MessageId);
                // Invalid envelopes must also reach DLQ without invoking a handler.
                await fixture.Channel.BasicPublishAsync(fixture.Settings.Exchange, "UserRegistered", mandatory: true,
                    basicProperties: new BasicProperties { MessageId = Guid.NewGuid().ToString(), Persistent = true }, body: "invalid-json"u8.ToArray());
                var invalid = await fixture.GetAsync(fixture.Queue("inbox") + ".dead");
                Assert.Equal("invalid-json", System.Text.Encoding.UTF8.GetString(invalid.Body.Span));
            }
            finally { await consumer.StopAsync(default); }
        }
        finally { await using var db = Context(); await db.Database.EnsureDeletedAsync(); }
    }

    public sealed class RecordingHandler(UserDbContext db) : IIntegrationEventHandler
    {
        public async Task HandleAsync(IntegrationEventEnvelope integrationEvent, CancellationToken ct)
        {
            db.Add(new OutboxMessage { EventType = "Effect", PayloadJson = "{}" });
            await db.SaveChangesAsync(ct);
            if (integrationEvent.Payload.TryGetProperty("fail", out var fail) && fail.GetBoolean())
                throw new InvalidOperationException("test rollback");
        }
    }

    private sealed class Broker : IAsyncDisposable
    {
        public required RabbitMqOptions Settings;
        public required IConnection NativeConnection;
        public required IChannel Channel;
        public required RabbitMqConnection Connections;
        public required RabbitMqPublisher Publisher;
        public RabbitMqOutboxTransport Transport => new(Publisher);
        private readonly List<string> queues = [];
        public string Queue(string suffix) => Settings.Exchange + "." + suffix;
        public static async Task<Broker> CreateAsync()
        {
            var configured = new ConnectionFactory { Uri = new Uri(Environment.GetEnvironmentVariable("TV1_TEST_RABBITMQ")!) };
            var settings = new RabbitMqOptions
            {
                HostName = configured.HostName, Port = configured.Port, UserName = configured.UserName,
                Password = configured.Password, VirtualHost = configured.VirtualHost, UseTls = configured.Ssl.Enabled,
                Exchange = "tv1.test." + Guid.NewGuid().ToString("N")
            };
            var host = new RabbitMqTransportTests.Host();
            var native = await settings.CreateFactory(host).CreateConnectionAsync();
            var channel = await native.CreateChannelAsync();
            var connections = new RabbitMqConnection(Options.Create(settings), host);
            return new Broker { Settings = settings, NativeConnection = native, Channel = channel, Connections = connections,
                Publisher = new RabbitMqPublisher(connections, Options.Create(settings)) };
        }
        public async Task SubscribeAsync(string suffix)
        {
            var queue = Queue(suffix); queues.Add(queue);
            await RabbitMqTopology.DeclareSubscriptionAsync(Channel, Settings.Exchange, queue, ["UserRegistered"], default);
        }
        public async Task<BasicGetResult> GetAsync(string queue)
        {
            using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (true)
            {
                var result = await Channel.BasicGetAsync(queue, autoAck: true, wait.Token);
                if (result is not null) return result;
                await Task.Delay(100, wait.Token);
            }
        }
        public async ValueTask DisposeAsync()
        {
            await Publisher.DisposeAsync(); await Connections.DisposeAsync();
            foreach (var queue in queues)
            {
                await Channel.QueueDeleteAsync(queue, false, false);
                await Channel.QueueDeleteAsync(queue + ".dead", false, false);
            }
            await Channel.ExchangeDeleteAsync(Settings.Exchange, false);
            if (queues.Count > 0) await Channel.ExchangeDeleteAsync(Settings.Exchange + ".dead", false);
            await Channel.DisposeAsync(); await NativeConnection.DisposeAsync();
        }
    }
}
