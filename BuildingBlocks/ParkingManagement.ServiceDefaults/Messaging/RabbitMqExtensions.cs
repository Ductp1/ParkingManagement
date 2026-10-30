using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ParkingManagement.ServiceDefaults.Persistence;

namespace ParkingManagement.ServiceDefaults.Messaging;

public static class RabbitMqExtensions
{
    public static IServiceCollection AddRabbitMqEventBus(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<RabbitMqOptions>().Bind(config.GetSection("EventBus:RabbitMq"));
        services.TryAddSingleton<RabbitMqConnection>();
        services.TryAddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        services.TryAddSingleton<RabbitMqOutboxTransport>();
        services.TryAddSingleton<IOutboxTransport>(sp => sp.GetRequiredService<RabbitMqOutboxTransport>());
        return services;
    }

    public static IServiceCollection AddRabbitMqInboxConsumer<TContext, THandler>(this IServiceCollection services, IConfiguration config)
        where TContext : ServiceDbContext where THandler : class, IIntegrationEventHandler
    {
        services.AddRabbitMqEventBus(config);
        services.AddOptions<RabbitMqSubscription>().Bind(config.GetSection("EventBus:Subscription"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Queue) && !o.Queue.StartsWith("amq.", StringComparison.Ordinal)
                && o.RoutingKeys.Length > 0 && o.RoutingKeys.All(k => !string.IsNullOrWhiteSpace(k)), "Consumer cần queue và routing keys.")
            .ValidateOnStart();
        services.AddScoped<THandler>();
        services.AddHostedService<RabbitMqInboxConsumer<TContext, THandler>>();
        return services;
    }
}
