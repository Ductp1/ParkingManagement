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

    public Task<bool> HasOverlappingBookingForVehicleAsync(int userId, int vehicleId, string plateNumber, DateTime startUtc, DateTime endUtc, int? excludeBookingId = null, CancellationToken cancellationToken = default)
    {
        var normalizedPlate = plateNumber.Trim().ToUpperInvariant();
        var query = db.Bookings.AsNoTracking().Where(b =>
            b.UserId == userId &&
            ActiveSlotStatuses.Contains(b.Status) &&
            ((vehicleId > 0 && b.VehicleId == vehicleId) || b.PlateNumber == normalizedPlate) &&
            b.StartAtUtc < endUtc && startUtc < b.EndAtUtc);

        if (excludeBookingId.HasValue)
        {
            query = query.Where(b => b.Id != excludeBookingId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public async Task<bool> TryCreateBookingWithLockAsync(Booking booking, int? slotId, CancellationToken cancellationToken = default)
    {
        var isNpgsql = db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
        if (!isNpgsql)
        {
            if (slotId.HasValue && slotId.Value > 0)
            {
                if (await HasOverlappingBookingForSlotAsync(slotId.Value, booking.StartAtUtc, booking.EndAtUtc, null, cancellationToken))
                    return false;
            }
            if (await HasOverlappingBookingForVehicleAsync(booking.UserId, booking.VehicleId, booking.PlateNumber, booking.StartAtUtc, booking.EndAtUtc, null, cancellationToken))
                return false;

            db.Bookings.Add(booking);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        // Khóa theo slot để ngăn chặn race condition khi nhiều người cùng đặt 1 ô cùng 1 mili-giây (BUG-BOOK-001)
        if (slotId.HasValue && slotId.Value > 0)
        {
            await db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock({0})", [(long)(2_000_000_000L + slotId.Value)], cancellationToken);

            if (await HasOverlappingBookingForSlotAsync(slotId.Value, booking.StartAtUtc, booking.EndAtUtc, null, cancellationToken))
            {
                return false;
            }
        }

        // Khóa theo vehicleId nếu có để ngăn cùng 1 xe đặt 2 booking trùng giờ
        if (booking.VehicleId > 0)
        {
            await db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock({0})", [(long)(1_000_000_000L + booking.VehicleId)], cancellationToken);
        }

        if (await HasOverlappingBookingForVehicleAsync(booking.UserId, booking.VehicleId, booking.PlateNumber, booking.StartAtUtc, booking.EndAtUtc, null, cancellationToken))
        {
            return false;
        }

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
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
