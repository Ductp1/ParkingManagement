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
}
