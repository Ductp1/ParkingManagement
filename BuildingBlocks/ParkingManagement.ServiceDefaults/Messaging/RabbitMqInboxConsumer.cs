using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingManagement.ServiceDefaults.Persistence;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ParkingManagement.ServiceDefaults.Messaging;

// Handler must use the same scoped DbContext and limit effects to that database transaction.
public interface IIntegrationEventHandler
{
    Task HandleAsync(IntegrationEventEnvelope integrationEvent, CancellationToken ct);
}

public sealed class RabbitMqInboxConsumer<TContext, THandler>(RabbitMqConnection connections,
    IOptions<RabbitMqOptions> options, IOptions<RabbitMqSubscription> subscription,
    IServiceScopeFactory scopes, ILogger<RabbitMqInboxConsumer<TContext, THandler>> logger) : BackgroundService
    where TContext : ServiceDbContext where THandler : class, IIntegrationEventHandler
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = subscription.Value;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var connection = await connections.GetAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(false, false, consumerDispatchConcurrency: 1), stoppingToken);
                await RabbitMqTopology.DeclareSubscriptionAsync(channel, options.Value.Exchange, settings.Queue, settings.RoutingKeys, stoppingToken);
                await channel.BasicQosAsync(0, Math.Clamp(settings.PrefetchCount, (ushort)1, (ushort)100), false, stoppingToken);
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                channel.ChannelShutdownAsync += (_, _) => { closed.TrySetResult(); return Task.CompletedTask; };
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    var committed = false;
                    try
                    {
                        // Deserialize before awaiting: RabbitMQ owns the delivery memory.
                        var envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope>(delivery.Body.Span, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                        if (envelope is null || envelope.EventId == Guid.Empty || string.IsNullOrWhiteSpace(envelope.EventType)
                            || envelope.EventType != delivery.RoutingKey || delivery.BasicProperties.MessageId != envelope.EventId.ToString())
                            throw new JsonException("Envelope hoặc MessageId không hợp lệ.");
                        await using var scope = scopes.CreateAsyncScope();
                        var db = scope.ServiceProvider.GetRequiredService<TContext>();
                        var handler = scope.ServiceProvider.GetRequiredService<THandler>();
                        await db.ApplyOnceAsync(envelope.EventId, ct => handler.HandleAsync(envelope, ct), stoppingToken);
                        committed = true;
                        await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { /* Unacked message is requeued when channel closes. */ }
                    catch (Exception ex)
                    {
                        if (committed)
                        {
                            logger.LogWarning("RabbitMQ ack chưa được xác nhận cho {Queue}; mở lại channel để nhận lại an toàn.", settings.Queue);
                            closed.TrySetResult();
                            return;
                        }
                        logger.LogWarning("RabbitMQ consumer {Queue} thất bại: {ErrorType}; chuyển vào dead-letter queue.", settings.Queue, ex.GetType().Name);
                        try { await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken); }
                        catch { closed.TrySetResult(); } // Broker disconnect: reopen channel, redelivery is deduplicated by Inbox.
                    }
                };
                await channel.BasicConsumeAsync(settings.Queue, autoAck: false, consumer, stoppingToken);
                await closed.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("RabbitMQ consumer chưa sẵn sàng: {ErrorType}.", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
