using BookingService.Application.Features.Bookings;
using BookingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;

namespace BookingService.Infrastructure.Persistence.Queries;

/// <summary>US-103: đếm tần suất đặt/hủy và thống kê No-show của một tài khoản trên bảng Bookings.</summary>
public sealed class BookingAbuseQueries(BookingDbContext db) : IBookingAbuseQueries
{
    public Task<int> CountCreatedSinceAsync(int userId, DateTime sinceUtc, CancellationToken cancellationToken = default)
        => db.Bookings.AsNoTracking()
            .Where(b => b.UserId == userId && b.CreatedAtUtc >= sinceUtc)
            .CountAsync(cancellationToken);

    // Đếm lượt "hủy" do CHÍNH khách thực hiện trong cửa sổ thời gian (US-103 AC1).
    // Không tính No-show (Staff/worker xử lý) và hủy do chủ bãi (CancelledByUserId khác khách).
    public Task<int> CountCancellationsSinceAsync(int userId, DateTime sinceUtc, CancellationToken cancellationToken = default)
        => db.Bookings.AsNoTracking()
            .Where(b => b.UserId == userId
                        && b.CancelledByUserId == userId
                        && b.CancelledAtUtc != null && b.CancelledAtUtc >= sinceUtc
                        && (b.Status == BookingStatus.Cancelled || b.Status == BookingStatus.CancelledNoRefund))
            .CountAsync(cancellationToken);

    public async Task<NoShowStatistics> GetNoShowStatisticsAsync(int userId, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        // Booking đã kết thúc trong kỳ nhìn lại: Completed / NoShow / CancelledNoRefund (khách chịu trách nhiệm).
        var finished = await db.Bookings.AsNoTracking()
            .Where(b => b.UserId == userId && b.CreatedAtUtc >= sinceUtc
                        && (b.Status == BookingStatus.Completed
                            || b.Status == BookingStatus.NoShow
                            || b.Status == BookingStatus.CancelledNoRefund))
            .CountAsync(cancellationToken);

        var noShows = await db.Bookings.AsNoTracking()
            .Where(b => b.UserId == userId && b.CreatedAtUtc >= sinceUtc && b.Status == BookingStatus.NoShow)
            .CountAsync(cancellationToken);

        return new NoShowStatistics(finished, noShows);
    }
}