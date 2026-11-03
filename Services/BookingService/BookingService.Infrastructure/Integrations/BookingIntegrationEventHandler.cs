using System.Text.Json;
using BookingService.Application.Features.Bookings;
using BookingService.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace BookingService.Infrastructure.Integrations;

/// <summary>US-083: outbox publisher — event ghi vào bảng OutboxMessages của BookingDbContext.</summary>
public sealed class BookingEventOutbox(BookingDbContext db) : IBookingEventPublisher
{
    public void Enqueue(IntegrationEvent integrationEvent)
        => db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = integrationEvent.GetType().Name,
            // Serialize theo RUNTIME type (không phải base IntegrationEvent) để đủ toàn bộ trường của event.
            PayloadJson = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
            OccurredAtUtc = DateTime.UtcNow
        });
}

/// <summary>
/// US-083: consumer BookingService — lắng nghe kết quả thanh toán để CHỐT vòng đời booking
/// (bridge còn thiếu trước đó: PaymentService bắn PaymentSucceeded nhưng không ai ConfirmPayment).
///  - PaymentSucceeded → booking.ConfirmPayment + phát BookingConfirmed (NotificationService gửi thông báo US-083/US-084 đặt lịch nhắc).
///  - PaymentFailed → không đổi trạng thái (HoldExpiryWorker sẽ hết hạn giữ chỗ 15 phút); NotificationService tự nhận PaymentFailed.
/// Idempotency: TransactionalInbox chặn xử lý trùng theo EventId; ConfirmPayment tự chặn re-confirm (ConflictException).
/// </summary>
public sealed class BookingIntegrationEventHandler(
    IBookingRepository repository,
    IBookingEventPublisher events,
    TimeProvider clock,
    ILogger<BookingIntegrationEventHandler> logger) : IIntegrationEventHandler
{
    public async Task HandleAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        switch (envelope.EventType)
        {
            case nameof(PaymentSucceeded):
                await OnPaymentSucceededAsync(
                    JsonSerializer.Deserialize<PaymentSucceeded>(envelope.Payload.GetRawText())
                        ?? throw new JsonException("Payload PaymentSucceeded rỗng."),
                    cancellationToken);
                break;
            case nameof(PaymentFailed):
                var failed = JsonSerializer.Deserialize<PaymentFailed>(envelope.Payload.GetRawText())
                             ?? throw new JsonException("Payload PaymentFailed rỗng.");
                logger.LogInformation("PaymentFailed (payment={PaymentId}, booking={BookingId}) — giữ chỗ sẽ hết hạn theo HoldExpiryWorker.",
                    failed.PaymentId, failed.BookingId);
                break;
            default:
                logger.LogInformation("Event {EventType} không thuộc phạm vi BookingService — bỏ qua.", envelope.EventType);
                break;
        }
    }

    private async Task OnPaymentSucceededAsync(PaymentSucceeded payload, CancellationToken cancellationToken)
    {
        if (payload.BookingId is null or <= 0)
        {
            // Thanh toán lượt gửi tại cổng (ParkingSessionId) — không thuộc vòng đời booking.
            return;
        }

        var booking = await repository.GetEntityByIdAsync(payload.BookingId.Value, cancellationToken);
        if (booking is null)
        {
            logger.LogWarning("PaymentSucceeded trỏ booking {BookingId} không tồn tại — bỏ qua.", payload.BookingId);
            return;
        }

        if (booking.Status is BookingStatus.PendingPayment or BookingStatus.PendingOwnerApproval)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            booking.ConfirmPayment(payload.Amount, now);
            events.Enqueue(new BookingConfirmed(
                booking.Id, booking.Code, booking.UserId, booking.ParkingLotId, booking.OwnerProfileId,
                booking.ParkingLotName, booking.SlotId, booking.SlotCode, booking.PlateNumber,
                booking.StartAtUtc, booking.EndAtUtc, booking.TotalAmount));
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Booking {Code} đã CONFIRMED sau thanh toán {PaymentId}.", booking.Code, payload.PaymentId);
        }
        else
        {
            logger.LogInformation("Booking {Code} ở trạng thái {Status} — bỏ qua PaymentSucceeded (idempotent).",
                booking.Code, booking.Status);
        }
    }
}
