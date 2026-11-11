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
/// US-083 + T-302 (merge develop): consumer BookingService — lắng nghe kết quả thanh toán để CHỐT vòng đời booking.
/// Lifecycle ủy quyền cho saga IProcessPaymentResultUseCase (develop T-302):
///  - PaymentSucceeded → idempotency (đã Confirmed/PendingOwnerApproval bỏ qua) · hết hạn giữ chỗ → Expired ·
///    bãi cần duyệt → PendingOwnerApproval · còn lại → ConfirmPayment.
///  - PaymentFailed → PendingPayment → Cancelled (FailPayment); trạng thái khác bỏ qua.
/// Sau khi saga chốt trạng thái, handler phát event làm giàu cho NotificationService:
///  - BookingConfirmed (chỉ khi vừa chuyển sang Confirmed — bãi Mức 0 chờ chủ bãi duyệt sẽ phát ở ReviewBookingUseCase).
///  - BookingPaymentCompleted (thành công/thất bại, có UserId + BookingCode — event gốc của PaymentService thiếu).
/// Idempotency: TransactionalInbox chặn xử lý trùng theo EventId; ConfirmPayment tự chặn re-confirm (ConflictException).
/// </summary>
public sealed class BookingIntegrationEventHandler(
    IBookingRepository repository,
    IProcessPaymentResultUseCase paymentResult,
    IBookingEventPublisher events,
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
                await OnPaymentFailedAsync(
                    JsonSerializer.Deserialize<PaymentFailed>(envelope.Payload.GetRawText())
                        ?? throw new JsonException("Payload PaymentFailed rỗng."),
                    cancellationToken);
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

        var statusBefore = booking.Status;

        // T-302 (merge develop): saga chốt vòng lifecycle — xử lý hết hạn giữ chỗ, chờ chủ bãi duyệt, ConfirmPayment.
        await paymentResult.ProcessPaymentSucceededAsync(payload, cancellationToken);

        // US-083/085: phát BookingConfirmed khi saga vừa chuyển sang Confirmed (bãi Mức 1-3 tự động).
        // Bãi Mức 0 (PendingOwnerApproval) KHÔNG phát ở đây — chủ bãi duyệt (ReviewBookingUseCase) mới phát,
        // để tránh booking chưa được duyệt đã nhận thông báo xác nhận.
        if (booking.Status is BookingStatus.Confirmed
            && statusBefore is BookingStatus.PendingPayment or BookingStatus.PendingOwnerApproval)
        {
            events.Enqueue(new BookingConfirmed(
                booking.Id, booking.Code, booking.UserId, booking.ParkingLotId, booking.OwnerProfileId,
                booking.ParkingLotName, booking.SlotId, booking.SlotCode, booking.PlateNumber,
                booking.StartAtUtc, booking.EndAtUtc, booking.TotalAmount));
        }

        // US-083: event làm giàu cho NotificationService (có UserId + BookingCode — event gốc của PaymentService thiếu).
        events.Enqueue(new BookingPaymentCompleted(
            payload.PaymentId, booking.Id, booking.Code, booking.UserId,
            payload.Amount, payload.Method, Succeeded: true, FailureReason: null));
        await repository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Booking {Code}: PaymentSucceeded {PaymentId} → saga {Before} → {After}; đã phát events US-083.",
            booking.Code, payload.PaymentId, statusBefore, booking.Status);
    }

    private async Task OnPaymentFailedAsync(PaymentFailed payload, CancellationToken cancellationToken)
    {
        if (payload.BookingId is null or <= 0) return;
        var booking = await repository.GetEntityByIdAsync(payload.BookingId.Value, cancellationToken);
        if (booking is null) return;

        var statusBefore = booking.Status;

        // T-302 (merge develop): saga xử lý thất bại — PendingPayment → Cancelled (FailPayment); trạng thái khác bỏ qua.
        await paymentResult.ProcessPaymentFailedAsync(payload, cancellationToken);

        // US-083: vẫn phát event thông báo thất bại cho NotificationService (dù booking đã Cancelled).
        events.Enqueue(new BookingPaymentCompleted(
            payload.PaymentId, booking.Id, booking.Code, booking.UserId,
            booking.TotalAmount, "VNPAY", Succeeded: false, FailureReason: payload.Reason));
        await repository.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Booking {Code}: thanh toán {PaymentId} THẤT BẠI → saga {Before} → {After}.",
            booking.Code, payload.PaymentId, statusBefore, booking.Status);
    }
}
