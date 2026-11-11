using BookingService.Application.Features.Bookings;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace BookingService.Infrastructure.Persistence.Queries;

/// <summary>US-067/069: đọc-only bảng Bookings cho ParkingService (dashboard + emergency fan-out).</summary>
public sealed class LotBookingQueries(BookingDbContext db) : ILotBookingSummaryQueries
{
    private static readonly BookingStatus[] ActiveStatuses = [BookingStatus.CheckedIn, BookingStatus.Parking];
    private static readonly BookingStatus[] NotCheckedInStatuses =
        [BookingStatus.PendingPayment, BookingStatus.PendingOwnerApproval, BookingStatus.Confirmed];

    public async Task<(int Active, int Upcoming)> GetCountsAsync(int lotId, CancellationToken cancellationToken = default)
    {
        var active = await db.Bookings.AsNoTracking()
            .CountAsync(b => b.ParkingLotId == lotId && ActiveStatuses.Contains(b.Status), cancellationToken);
        var upcoming = await db.Bookings.AsNoTracking()
            .CountAsync(b => b.ParkingLotId == lotId && NotCheckedInStatuses.Contains(b.Status), cancellationToken);
        return (active, upcoming);
    }

    public async Task<IReadOnlyList<EmergencyAffectedBooking>> GetAffectedAsync(int lotId, CancellationToken cancellationToken = default)
        => await db.Bookings.AsNoTracking()
            .Where(b => b.ParkingLotId == lotId && NotCheckedInStatuses.Contains(b.Status))
            .OrderBy(b => b.StartAtUtc)
            .Select(b => new EmergencyAffectedBooking(
                b.Id, b.Code, b.UserId, b.Status.ToString(), b.StartAtUtc, b.EndAtUtc))
            .ToListAsync(cancellationToken);
}