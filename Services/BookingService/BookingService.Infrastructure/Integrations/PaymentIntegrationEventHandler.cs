using System.Text.Json;
using BookingService.Application.Features.Bookings;
using Microsoft.Extensions.Logging;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.SharedKernel.Contracts;

namespace BookingService.Infrastructure.Integrations;

/// <summary>
/// T-302: Handler nhận sự kiện thanh toán từ PaymentService qua RabbitMQ Inbox Consumer.
/// Điều phối luồng xử lý Saga phân tán cho BookingService.
/// </summary>
public sealed class PaymentIntegrationEventHandler(
    IProcessPaymentResultUseCase useCase,
    ILogger<PaymentIntegrationEventHandler> logger) : IIntegrationEventHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task HandleAsync(IntegrationEventEnvelope envelope, CancellationToken ct)
    {
        logger.LogInformation("Nhận integration event: {EventType} (ID: {EventId})", envelope.EventType, envelope.EventId);

        if (string.Equals(envelope.EventType, nameof(PaymentSucceeded), StringComparison.OrdinalIgnoreCase)
            || string.Equals(envelope.EventType, "payment.succeeded", StringComparison.OrdinalIgnoreCase))
        {
            var @event = envelope.Payload.Deserialize<PaymentSucceeded>(JsonOptions);
            if (@event != null)
            {
                await useCase.ProcessPaymentSucceededAsync(@event, ct);
                logger.LogInformation("Đã xử lý PaymentSucceeded thành công cho BookingId: {BookingId}", @event.BookingId);
            }
        }
        else if (string.Equals(envelope.EventType, nameof(PaymentFailed), StringComparison.OrdinalIgnoreCase)
            || string.Equals(envelope.EventType, "payment.failed", StringComparison.OrdinalIgnoreCase))
        {
            var @event = envelope.Payload.Deserialize<PaymentFailed>(JsonOptions);
            if (@event != null)
            {
                await useCase.ProcessPaymentFailedAsync(@event, ct);
                logger.LogInformation("Đã xử lý PaymentFailed cho BookingId: {BookingId}, Lý do: {Reason}", @event.BookingId, @event.Reason);
            }
        }
        else
        {
            logger.LogDebug("Bỏ qua event không thuộc phạm vi xử lý của Booking Saga: {EventType}", envelope.EventType);
        }
    }
}

