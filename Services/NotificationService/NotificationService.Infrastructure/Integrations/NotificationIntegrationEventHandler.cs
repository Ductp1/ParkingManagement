using System.Text.Json;
using Microsoft.Extensions.Logging;
using ParkingManagement.ServiceDefaults.Messaging;

namespace NotificationService.Infrastructure.Integrations;

/// <summary>
/// US-083→086: consumer duy nhất của NotificationService — MassTransit route message theo routing key
/// (= EventType) về handler này, handler switch EventType rồi gọi use case tương ứng.
/// Idempotency lớp 1: TransactionalInbox.ApplyOnceAsync chặn xử lý trùng theo EventId (CÙNG transaction
/// với mọi thay đổi DB — MassTransit chỉ ack sau commit, redelivery an toàn).
/// Payload deserialize bằng JsonSerializerDefaults.Web (case-insensitive) nên khớp cả PascalCase của outbox.
/// </summary>
public sealed class NotificationIntegrationEventHandler(ILogger<NotificationIntegrationEventHandler> logger) : IIntegrationEventHandler
{
    public Task HandleAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        // Foundation (F): đường ống sự kiện được kiểm chứng — queue/subscription/inbox vận hành.
        // US-083 bổ sung case BookingConfirmed/PaymentSucceeded/PaymentFailed,
        // US-084 case BookingConfirmed (đặt lịch)/BookingCancelled (hủy lịch)/BookingModified (reschedule),
        // US-085 case BookingCreated/Cancelled/Modified (báo chủ bãi),
        // US-086 case LotEmergencyStopped (fan-out đa kênh).
        logger.LogInformation("Event {EventType} (eventId={EventId}) đến NotificationService — chưa gắn nghiệp vụ, bỏ qua an toàn.",
            envelope.EventType, envelope.EventId);
        return Task.CompletedTask;
    }
}
