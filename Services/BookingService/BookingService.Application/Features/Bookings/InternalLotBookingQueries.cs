using ParkingManagement.SharedKernel.Contracts;

namespace BookingService.Application.Features.Bookings;

/// <summary>
/// US-067/069: tra cứu booking theo bãi cho ParkingService (internal HTTP, không auth).
/// Đọc-only — không đổi trạng thái booking.
/// </summary>
public interface ILotBookingSummaryQueries
{
    /// <summary>
    /// Dashboard counts (US-067):
    /// Active = đang trong bãi (CheckedIn / Parking);
    /// Upcoming = chưa check-in (PendingPayment / PendingOwnerApproval / Confirmed).
    /// </summary>
    Task<(int Active, int Upcoming)> GetCountsAsync(int lotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Booking bị ảnh hưởng khi đóng bãi khẩn cấp (US-069 → US-086 fan-out):
    /// chưa check-in (PendingPayment / PendingOwnerApproval / Confirmed).
    /// </summary>
    Task<IReadOnlyList<EmergencyAffectedBooking>> GetAffectedAsync(int lotId, CancellationToken cancellationToken = default);
}