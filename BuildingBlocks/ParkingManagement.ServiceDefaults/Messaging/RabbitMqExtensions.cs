using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ParkingManagement.ServiceDefaults.Persistence;
using RabbitMQ.Client;

namespace ParkingManagement.ServiceDefaults.Messaging;

public static class RabbitMqExtensions
{
    public static IServiceCollection AddRabbitMqEventBus(this IServiceCollection services, IConfiguration config)
    {
        if (config.GetValue<bool?>("EventBus:Enabled") is not false)
            return AddBus(services, config, null, null);
        // Disabling the UserService Outbox must not start a broker connection at app startup.
        services.TryAddSingleton<IRabbitMqPublisher, DisabledPublisher>();
        return AddTransport(services);
    }

    public static IServiceCollection AddRabbitMqInboxConsumer<TContext, THandler>(this IServiceCollection services, IConfiguration config)
        where TContext : ServiceDbContext where THandler : class, IIntegrationEventHandler
    {
        services.AddOptions<RabbitMqSubscription>().Bind(config.GetSection("EventBus:Subscription"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Queue) && !o.Queue.StartsWith("amq.", StringComparison.Ordinal)
                && o.RoutingKeys.Length > 0 && o.RoutingKeys.All(k => !string.IsNullOrWhiteSpace(k)), "Consumer cần queue và routing keys.")
            .ValidateOnStart();
        services.AddScoped<THandler>();
        return AddBus(services, config, x => x.AddConsumer<RabbitMqInboxConsumer<TContext, THandler>>(), (context, bus) =>
        {
            var subscription = context.GetRequiredService<IOptions<RabbitMqSubscription>>().Value;
            var rabbit = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            bus.ReceiveEndpoint(subscription.Queue, endpoint =>
            {
                endpoint.ConfigureConsumeTopology = false;
                endpoint.SetQuorumQueue();
                endpoint.SetQueueArgument("x-dead-letter-exchange", rabbit.Exchange + ".dead");
                endpoint.SetQueueArgument("x-dead-letter-routing-key", subscription.Queue);
                endpoint.PrefetchCount = Math.Clamp(subscription.PrefetchCount, (ushort)1, (ushort)100);
                endpoint.ConcurrentMessageLimit = 1;
                endpoint.UseRawJsonDeserializer(RawSerializerOptions.All, isDefault: true);
                endpoint.UseMessageRetry(retry => { retry.Ignore<JsonException>(); retry.Immediate(3); });
                endpoint.PublishFaults = false; // Error messages remain in <queue>_error; do not broadcast payloads.
                foreach (var key in subscription.RoutingKeys.Distinct())
                    endpoint.Bind(rabbit.Exchange, binding => { binding.ExchangeType = ExchangeType.Topic; binding.RoutingKey = key; });
                endpoint.ConfigureConsumer<RabbitMqInboxConsumer<TContext, THandler>>(context);
            });
        });
    }

    private static IServiceCollection AddBus(IServiceCollection services, IConfiguration config,
        Action<IBusRegistrationConfigurator>? consumers,
        Action<IBusRegistrationContext, IRabbitMqBusFactoryConfigurator>? endpoints)
    {
        services.AddOptions<RabbitMqOptions>().Bind(config.GetSection("EventBus:RabbitMq"));
        services.Configure<MassTransitHostOptions>(o => { o.WaitUntilStarted = false; o.StopTimeout = TimeSpan.FromSeconds(10); });
        services.AddMassTransit(registration =>
        {
            consumers?.Invoke(registration);
            registration.UsingRabbitMq((context, bus) =>
            {
                var rabbit = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
                rabbit.Validate(context.GetRequiredService<IHostEnvironment>()); // Shared credential/TLS validation.
                bus.Host(rabbit.HostName, (ushort)rabbit.Port, rabbit.VirtualHost, host =>
                {
                    host.Username(rabbit.UserName); host.Password(rabbit.Password);
                    host.PublisherConfirmation = true;
                    host.RequestedConnectionTimeout(TimeSpan.FromSeconds(rabbit.OperationTimeoutSeconds));
                    if (rabbit.UseTls) host.UseSsl(ssl => ssl.ServerName = rabbit.HostName);
                });
                bus.UseRawJsonSerializer(); // Preserve the existing eventId/eventType/payload wire format.
                endpoints?.Invoke(context, bus);
            });
        });
        services.TryAddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        return AddTransport(services);
    }

    private static IServiceCollection AddTransport(IServiceCollection services)
    {
        services.TryAddSingleton<RabbitMqOutboxTransport>();
        services.TryAddSingleton<IOutboxTransport>(sp => sp.GetRequiredService<RabbitMqOutboxTransport>());
        return services;
    }

    private sealed class DisabledPublisher : IRabbitMqPublisher
    {
        public Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken ct)
            => Task.FromException(new InvalidOperationException("EventBus đang bị tắt."));
    }
}
