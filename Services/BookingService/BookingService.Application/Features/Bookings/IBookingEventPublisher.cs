using ParkingManagement.SharedKernel.Contracts;

namespace BookingService.Application.Features.Bookings;

/// <summary>
/// US-083/084/085: cổng phát integration event. Implementation ghi OutboxMessage vào CÙNG DbContext
/// của use case → commit trong cùng transaction với thay đổi booking (transactional outbox).
/// EventType = tên class của record, wire format = JSON PascalCase (khớp PaymentService đang có).
/// </summary>
public interface IBookingEventPublisher
{
    void Enqueue(IntegrationEvent integrationEvent);
}
