using BookingService.Application.Features.Bookings;
using BookingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace BookingService.Infrastructure.Persistence.Repositories;

public sealed class BookingRepository(BookingDbContext db) : IBookingRepository
{
    private static readonly BookingStatus[] ActiveSlotStatuses =
    [
        BookingStatus.PendingPayment,
        BookingStatus.PendingOwnerApproval,
        BookingStatus.Confirmed,
        BookingStatus.CheckedIn,
        BookingStatus.Parking
    ];

    public Task<Booking?> GetEntityByCodeAsync(string code, CancellationToken cancellationToken = default)
        => db.Bookings
            .Include(b => b.StatusLogs)
            .Include(b => b.Modifications)
            .Include(b => b.PriceSnapshot)
            .FirstOrDefaultAsync(b => b.Code == code, cancellationToken);

    public Task<Booking?> GetEntityByIdAsync(int id, CancellationToken cancellationToken = default)
        => db.Bookings
            .Include(b => b.StatusLogs)
            .Include(b => b.Modifications)
            .Include(b => b.PriceSnapshot)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<bool> HasOverlappingBookingForSlotAsync(int slotId, DateTime startUtc, DateTime endUtc, int? excludeBookingId = null, CancellationToken cancellationToken = default)
    {
        var query = db.Bookings.AsNoTracking().Where(b =>
            b.SlotId == slotId &&
            ActiveSlotStatuses.Contains(b.Status) &&
            b.StartAtUtc < endUtc && startUtc < b.EndAtUtc);

        if (excludeBookingId.HasValue)
        {
            query = query.Where(b => b.Id != excludeBookingId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> HasOverlappingBookingForUserAsync(int userId, DateTime startUtc, DateTime endUtc, int? excludeBookingId = null, CancellationToken cancellationToken = default)
    {
        var query = db.Bookings.AsNoTracking().Where(b =>
            b.UserId == userId &&
            ActiveSlotStatuses.Contains(b.Status) &&
            b.StartAtUtc < endUtc && startUtc < b.EndAtUtc);

        if (excludeBookingId.HasValue)
        {
            query = query.Where(b => b.Id != excludeBookingId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> IsSlotAssignedToActiveMonthlyPassAsync(int slotId, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return db.MonthlyPasses.AsNoTracking().AnyAsync(m =>
            m.SlotId == slotId &&
            m.Status == MonthlyPassStatus.Active &&
            m.ValidTo >= today, cancellationToken);
    }

    public Task<int> CountCancellationsTodayAsync(int userId, DateTime todayUtc, CancellationToken cancellationToken = default)
    {
        return db.Bookings.AsNoTracking().CountAsync(b =>
            b.UserId == userId &&
            (b.Status == BookingStatus.Cancelled || b.Status == BookingStatus.CancelledNoRefund) &&
            b.CancelledAtUtc >= todayUtc, cancellationToken);
    }

    public Task AddOutboxMessageAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default)
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = eventType,
            PayloadJson = payloadJson,
            OccurredAtUtc = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }

    public async Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        await db.Bookings.AddAsync(booking, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
