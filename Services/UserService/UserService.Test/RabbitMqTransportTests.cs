using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;

namespace UserService.Test;

public sealed class RabbitMqTransportTests
{
    [Fact]
    public async Task Explicitly_disabled_producer_does_not_start_a_bus_or_silently_drop_events()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["EventBus:Enabled"] = "false" }).Build();
        using var services = new ServiceCollection().AddRabbitMqEventBus(config).BuildServiceProvider(true);
        Assert.Null(services.GetService<MassTransit.IBus>());
        Assert.Empty(services.GetServices<IHostedService>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<IOutboxTransport>()
            .DeliverAsync(new OutboxMessage { EventType = "UserRegistered", PayloadJson = "{}" }, default));
    }
    [Fact]
    public async Task Retries_preserve_event_identity_routing_key_and_envelope()
    {
        var publisher = new Publisher();
        var transport = new RabbitMqOutboxTransport(publisher);
        var message = new OutboxMessage { EventType = "UserRegistered", PayloadJson = "{\"UserId\":7}" };
        await Assert.ThrowsAsync<IOException>(() => transport.DeliverAsync(message, default));
        publisher.Fail = false;
        await transport.DeliverAsync(message, default);
        Assert.Equal(2, publisher.Messages.Count);
        foreach (var (eventId, type, body) in publisher.Messages)
        {
            Assert.Equal(message.Id, eventId); Assert.Equal(message.EventType, type);
            using var json = JsonDocument.Parse(body);
            Assert.Equal(message.Id, json.RootElement.GetProperty("eventId").GetGuid());
            Assert.Equal("UserRegistered", json.RootElement.GetProperty("eventType").GetString());
            Assert.Equal(7, json.RootElement.GetProperty("payload").GetProperty("UserId").GetInt32());
        }
    }
    [Fact]
    public async Task Invalid_json_never_reaches_broker()
    {
        var publisher = new Publisher();
        await Assert.ThrowsAnyAsync<JsonException>(() => new RabbitMqOutboxTransport(publisher)
            .DeliverAsync(new OutboxMessage { EventType = "UserRegistered", PayloadJson = "invalid" }, default));
        Assert.Empty(publisher.Messages);
    }
    [Fact]
    public async Task Missing_event_identity_is_rejected()
    {
        var publisher = new Publisher();
        await Assert.ThrowsAsync<ArgumentException>(() => new RabbitMqOutboxTransport(publisher)
            .DeliverAsync(new OutboxMessage { Id = Guid.Empty, EventType = "UserRegistered", PayloadJson = "{}" }, default));
        Assert.Empty(publisher.Messages);
    }
    [Fact]
    public void Remote_production_connections_require_tls_and_configured_credentials()
    {
        var settings = new RabbitMqOptions { HostName = "broker.example.test", Password = "test-only" };
        Assert.Throws<DependencyUnavailableException>(() => settings.Validate(new Host()));
        settings.UseTls = true;
        settings.Validate(new Host());
        settings.Password = "";
        Assert.Throws<DependencyUnavailableException>(() => settings.Validate(new Host()));
    }
    [Fact]
    public void Loopback_allows_development_without_tls_and_rejects_unbounded_timeouts()
    {
        var settings = new RabbitMqOptions { Password = "test-only" };
        settings.Validate(new Host());
        settings.OperationTimeoutSeconds = 0;
        Assert.Throws<DependencyUnavailableException>(() => settings.Validate(new Host()));
        settings.OperationTimeoutSeconds = 61;
        Assert.Throws<DependencyUnavailableException>(() => settings.Validate(new Host()));
    }
    private sealed class Publisher : IRabbitMqPublisher
    {
        public bool Fail = true;
        public List<(Guid Id, string Type, byte[] Body)> Messages = [];
        public Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken ct)
        {
            Messages.Add((envelope.EventId, envelope.EventType, JsonSerializer.SerializeToUtf8Bytes(envelope,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))));
            if (Fail) throw new IOException("broker unavailable");
            return Task.CompletedTask;
        }
    }
    internal sealed class Host : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "UserService.Test";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
