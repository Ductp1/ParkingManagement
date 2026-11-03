using System.Text.Json;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Features.Ingestion;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.SharedKernel.Contracts;

namespace NotificationService.Infrastructure.Integrations;

/// <summary>
/// US-083→086: consumer duy nhất của NotificationService — MassTransit route message theo routing key
/// (= EventType) về handler này, handler switch EventType rồi gọi use case tương ứng.
/// Idempotency lớp 1: TransactionalInbox.ApplyOnceAsync chặn xử lý trùng theo EventId (CÙNG transaction
/// với mọi thay đổi DB — MassTransit chỉ ack sau commit, redelivery an toàn).
/// Payload deserialize bằng JsonSerializerDefaults.Web (case-insensitive) nên khớp cả PascalCase của outbox.
/// </summary>
public sealed class NotificationIntegrationEventHandler(
    IBookingConfirmationNotifications bookingConfirmations,
    ILogger<NotificationIntegrationEventHandler> logger) : IIntegrationEventHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task HandleAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        switch (envelope.EventType)
        {
            // ===== US-083: xác nhận booking & thanh toán =====
            case nameof(BookingConfirmed):
                await bookingConfirmations.HandleBookingConfirmedAsync(
                    Deserialize<BookingConfirmed>(envelope), envelope.EventId, cancellationToken);
                break;
            case nameof(BookingPaymentCompleted):
                await bookingConfirmations.HandleBookingPaymentCompletedAsync(
                    Deserialize<BookingPaymentCompleted>(envelope), envelope.EventId, cancellationToken);
                break;

            // US-084 bổ sung: BookingConfirmed (đặt lịch nhắc) / BookingCancelled (hủy lịch) / BookingModified (reschedule).
            // US-085 bổ sung: BookingCreated/BookingCancelled/BookingModified (báo chủ bãi).
            // US-086 bổ sung: LotEmergencyStopped (fan-out đa kênh + tracking).
            default:
                logger.LogInformation("Event {EventType} (eventId={EventId}) chưa có handler nghiệp vụ — bỏ qua an toàn.",
                    envelope.EventType, envelope.EventId);
                break;
        }
    }

    private static T Deserialize<T>(IntegrationEventEnvelope envelope) where T : class
        => JsonSerializer.Deserialize<T>(envelope.Payload.GetRawText(), Json)
           ?? throw new JsonException($"Payload {envelope.EventType} rỗng hoặc sai định dạng.");
}
