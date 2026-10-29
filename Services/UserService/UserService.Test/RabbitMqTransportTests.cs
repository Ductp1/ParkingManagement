using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;

namespace UserService.Test;

public sealed class RabbitMqTransportTests
{
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
        Assert.Throws<DependencyUnavailableException>(() => settings.CreateFactory(new Host()));
        settings.UseTls = true;
        var factory = settings.CreateFactory(new Host());
        Assert.True(factory.Ssl.Enabled); Assert.Equal(settings.HostName, factory.Ssl.ServerName);
        Assert.Equal(System.Net.Security.SslPolicyErrors.None, factory.Ssl.AcceptablePolicyErrors);
        settings.Password = "";
        Assert.Throws<DependencyUnavailableException>(() => settings.CreateFactory(new Host()));
    }
    [Fact]
    public void Loopback_uses_bounded_timeouts_and_explicit_recovery()
    {
        var factory = new RabbitMqOptions { Password = "test-only" }.CreateFactory(new Host());
        Assert.False(factory.AutomaticRecoveryEnabled);
        Assert.Equal(TimeSpan.FromSeconds(10), factory.RequestedConnectionTimeout);
        Assert.Equal("parking", factory.VirtualHost);
    }
    private sealed class Publisher : IRabbitMqPublisher
    {
        public bool Fail = true;
        public List<(Guid Id, string Type, byte[] Body)> Messages = [];
        public Task PublishAsync(OutboxMessage message, ReadOnlyMemory<byte> body, CancellationToken ct)
        {
            Messages.Add((message.Id, message.EventType, body.ToArray()));
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
