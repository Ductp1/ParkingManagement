using System.Text.Json;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Features.Ingestion;
using NotificationService.Application.Features.Reminders;
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
    IBookingReminderScheduler reminderScheduler,
    IOwnerBookingNotifications ownerNotifications,
    ILogger<NotificationIntegrationEventHandler> logger) : IIntegrationEventHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task HandleAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        switch (envelope.EventType)
        {
            // ===== US-083 + US-084 + US-085: booking được xác nhận =====
            case nameof(BookingConfirmed):
            {
                var evt = Deserialize<BookingConfirmed>(envelope);
                await bookingConfirmations.HandleBookingConfirmedAsync(evt, envelope.EventId, cancellationToken);
                await reminderScheduler.ScheduleForBookingAsync(evt, cancellationToken);
                break;
            }
            case nameof(BookingPaymentCompleted):
                await bookingConfirmations.HandleBookingPaymentCompletedAsync(
                    Deserialize<BookingPaymentCompleted>(envelope), envelope.EventId, cancellationToken);
                break;

            // ===== US-084 (hủy/reschedule nhắc lịch) + US-085 (báo chủ bãi) =====
            case nameof(BookingCancelled):
            {
                var evt = Deserialize<BookingCancelled>(envelope);
                await reminderScheduler.CancelForBookingAsync(evt, cancellationToken);
                await ownerNotifications.HandleBookingCancelledAsync(evt, envelope.EventId, cancellationToken);
                break;
            }
            case nameof(BookingModified):
            {
                var evt = Deserialize<BookingModified>(envelope);
                await reminderScheduler.RescheduleForBookingAsync(evt, cancellationToken);
                await ownerNotifications.HandleBookingModifiedAsync(evt, envelope.EventId, cancellationToken);
                break;
            }

            // ===== US-085: booking mới tạo (PendingPayment) → báo chủ bãi =====
            case nameof(BookingCreated):
                await ownerNotifications.HandleBookingCreatedAsync(
                    Deserialize<BookingCreated>(envelope), envelope.EventId, cancellationToken);
                break;

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
