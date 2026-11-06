using BookingService.Application.Features.Bookings;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;

namespace BookingService.Infrastructure.Persistence.Queries;

public sealed class BookingQueries(BookingDbContext db) : IBookingQueries
{
    public Task<BookingDetailDto?> GetByCodeAsync(string code, CancellationToken cancellationToken)
        => db.Bookings.AsNoTracking()
            .Where(b => b.Code == code)
            .Select(b => new BookingDetailDto(
                b.Id, b.Code, b.Status.ToString(), b.UserId, b.VehicleId, b.PlateNumber, b.VehicleType.ToString(),
                b.ParkingLotId, b.ParkingLotName, b.ZoneId, b.SlotId, b.SlotCode, b.StartAtUtc, b.EndAtUtc,
                b.HoldExpiresAtUtc, b.TotalAmount, b.DiscountAmount, b.PaidAmount, b.PromotionCode,
                b.PriceSnapshot == null ? null : new PriceSnapshotDto(
                    b.PriceSnapshot.RateCardId, b.PriceSnapshot.BaseAmount, b.PriceSnapshot.SurchargeAmount,
                    b.PriceSnapshot.DiscountAmount, b.PriceSnapshot.FinalAmount, b.PriceSnapshot.BillingGracePeriodMinutes,
                    b.PriceSnapshot.RateCardJson),
                b.StatusLogs.OrderBy(l => l.CreatedAtUtc)
                    .Select(l => new StatusLogDto(l.FromStatus.ToString(), l.ToStatus.ToString(), l.ChangedByUserId, l.Reason, l.CreatedAtUtc))
                    .ToList(),
                b.QrToken))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<BookingSummaryDto>> ListByUserAsync(int userId, BookingStatus? status, CancellationToken cancellationToken)
    {
        var query = db.Bookings.AsNoTracking().Where(b => b.UserId == userId);
        if (status is { } s) query = query.Where(b => b.Status == s);

        return await query
            .OrderByDescending(b => b.StartAtUtc)
            .Select(b => new BookingSummaryDto(b.Id, b.Code, b.Status.ToString(), b.ParkingLotName, b.SlotCode,
                b.PlateNumber, b.StartAtUtc, b.EndAtUtc, b.TotalAmount))
            .ToListAsync(cancellationToken);
    }

    // UC-20 (US-076): booking trong ca trực của Staff — IsOverdue tính ở SQL (dịch được sang Postgres).
    public async Task<IReadOnlyList<GateBookingListItemDto>> ListForGateAsync(int parkingLotId, DateTime fromUtc, DateTime toUtc,
        DateTime nowUtc, int gracePeriodMinutes, string? search, CancellationToken cancellationToken)
    {
        // Booking sắp đến trong ca (Confirmed theo khung giờ bắt đầu) + xe đang trong bãi + no-show vừa xảy ra trong ca.
        BookingStatus[] statuses = [BookingStatus.Confirmed, BookingStatus.CheckedIn, BookingStatus.Parking, BookingStatus.NoShow];
        var query = db.Bookings.AsNoTracking()
            .Where(b => b.ParkingLotId == parkingLotId && statuses.Contains(b.Status)
                     && ((b.StartAtUtc >= fromUtc && b.StartAtUtc <= toUtc) || b.Status != BookingStatus.Confirmed));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToUpperInvariant();
            query = query.Where(b => b.Code.ToUpper().Contains(keyword) || b.PlateNumber.ToUpper().Contains(keyword));
        }

        return await query
            .OrderBy(b => b.StartAtUtc)
            .Select(b => new GateBookingListItemDto(
                b.Code, b.PlateNumber, b.VehicleType.ToString(), b.SlotCode, b.StartAtUtc, b.EndAtUtc,
                b.Status.ToString(),
                b.Status == BookingStatus.Confirmed && nowUtc > b.StartAtUtc.AddMinutes(gracePeriodMinutes),
                b.TotalAmount))
            .ToListAsync(cancellationToken);
    }
}
