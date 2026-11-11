using BookingService.Application.Features.Bookings;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;

namespace BookingService.Infrastructure.Persistence.Queries;

public sealed class BookingQueries(BookingDbContext db) : IBookingQueries, INoShowCandidateQueries
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

    // US-039 (UC-24): booking Confirmed, chưa check-in, đã quá mốc ân hạn kể từ giờ bắt đầu – ứng viên No-show của bãi.
    public async Task<IReadOnlyList<NoShowCandidateDto>> ListNoShowCandidatesAsync(
        int parkingLotId, DateTime nowUtc, int graceMinutes, CancellationToken cancellationToken)
    {
        var graceDeadline = nowUtc.AddMinutes(-graceMinutes);
        var rows = await db.Bookings.AsNoTracking()
            .Where(b => b.ParkingLotId == parkingLotId
                        && b.Status == BookingStatus.Confirmed
                        && b.CheckedInAtUtc == null
                        && b.StartAtUtc < graceDeadline)
            .OrderBy(b => b.StartAtUtc)
            .Select(b => new NoShowCandidateDto(
                b.Code, b.UserId, b.PlateNumber, b.ParkingLotId, b.ParkingLotName, b.SlotCode,
                b.StartAtUtc, b.EndAtUtc, b.TotalAmount, 0))
            .ToListAsync(cancellationToken);

        // Số phút quá ân hạn tính trong bộ nhớ để tránh dịch hằng thời gian phức tạp xuống SQL.
        return rows
            .Select(r => r with { MinutesOverdue = (int)Math.Max(0, (nowUtc - r.StartAtUtc).TotalMinutes) })
            .ToList();
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

    // T-303: Tra cứu lịch sử duyệt/từ chối đơn đặt chỗ bãi Mức 0 từ BookingStatusLogs.
    public async Task<IReadOnlyList<ApprovalLogDto>> ListApprovalLogsAsync(
        int? parkingLotId, DateTime? fromUtc, DateTime? toUtc, string? result, CancellationToken cancellationToken = default)
    {
        var query = db.BookingStatusLogs.AsNoTracking()
            .Include(l => l.Booking)
            .Where(l => l.FromStatus == BookingStatus.PendingOwnerApproval
                     && (l.ToStatus == BookingStatus.Confirmed || l.ToStatus == BookingStatus.Cancelled));

        if (parkingLotId.HasValue && parkingLotId > 0)
        {
            query = query.Where(l => l.Booking.ParkingLotId == parkingLotId.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(l => l.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(l => l.CreatedAtUtc <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(result))
        {
            var trimmed = result.Trim().ToUpperInvariant();
            if (trimmed == "APPROVED")
            {
                query = query.Where(l => l.ToStatus == BookingStatus.Confirmed);
            }
            else if (trimmed == "REJECTED")
            {
                query = query.Where(l => l.ToStatus == BookingStatus.Cancelled);
            }
        }

        return await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Select(l => new ApprovalLogDto(
                l.Id,
                l.BookingId,
                l.Booking.Code,
                l.Booking.PlateNumber,
                l.Booking.ParkingLotId,
                l.Booking.ParkingLotName,
                l.ToStatus == BookingStatus.Confirmed ? "Approved" : "Rejected",
                l.ChangedByUserId,
                l.Reason,
                l.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
