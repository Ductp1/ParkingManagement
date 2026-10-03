using BookingService.Application.Features.Bookings;
using BookingService.Domain.Rules;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace BookingService.Test;

/// <summary>TC-BOOK-07 / TC-BOOK-08 (Kế hoạch kiểm thử v3): test chính xác mốc biên 61 phút và 59 phút.</summary>
public class CancellationPolicyTests
{
    private static readonly DateTime Start = new(2026, 10, 3, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Cancel_61_minutes_before_refunds_100_percent()
    {
        var d = CancellationPolicy.Evaluate(BookingStatus.Confirmed, Start, Start.AddMinutes(-61));
        Assert.True(d.CanCancel);
        Assert.Equal(BookingStatus.Cancelled, d.ResultStatus);
        Assert.Equal(100, d.RefundPercent);
    }

    [Fact]
    public void Cancel_exactly_60_minutes_before_still_refunds_100_percent()
    {
        var d = CancellationPolicy.Evaluate(BookingStatus.Confirmed, Start, Start.AddMinutes(-60));
        Assert.Equal(100, d.RefundPercent);
    }

    [Fact]
    public void Cancel_59_minutes_before_refunds_nothing()
    {
        var d = CancellationPolicy.Evaluate(BookingStatus.Confirmed, Start, Start.AddMinutes(-59));
        Assert.True(d.CanCancel);
        Assert.Equal(BookingStatus.CancelledNoRefund, d.ResultStatus);
        Assert.Equal(0, d.RefundPercent);
    }

    [Theory]
    [InlineData(BookingStatus.CheckedIn)]
    [InlineData(BookingStatus.Completed)]
    [InlineData(BookingStatus.Cancelled)]
    public void Booking_already_in_progress_or_finished_cannot_be_cancelled(BookingStatus status)
        => Assert.False(CancellationPolicy.Evaluate(status, Start, Start.AddDays(-1)).CanCancel);

    [Fact]
    public async Task Preview_uses_paid_amount_and_current_time()
    {
        var booking = SampleBooking(status: "Confirmed", paid: 63000, start: Start);
        var clock = new FixedClock(Start.AddHours(-2));
        var preview = await new PreviewCancellationUseCase(new FakeQueries(booking), clock).ExecuteAsync("bk-0002");

        Assert.Equal(100, preview.RefundPercent);
        Assert.Equal(63000, preview.RefundAmount);
    }

    [Fact]
    public async Task Unknown_booking_code_throws_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetBookingByCodeUseCase(new FakeQueries(null)).ExecuteAsync("BK-9999"));

    // ---------- test doubles ----------
    private static BookingDetailDto SampleBooking(string status, decimal paid, DateTime start) => new(
        2, "BK-0002", status, 5, 1, "51F12345", "Sedan", 1, "Bãi xe Vincom Đồng Khởi", 2, "B1-A02",
        start, start.AddHours(3), null, 70000, 7000, paid, "WELCOME10", null, []);

    private sealed class FakeQueries(BookingDetailDto? booking) : IBookingQueries
    {
        public Task<BookingDetailDto?> GetByCodeAsync(string code, CancellationToken cancellationToken) => Task.FromResult(booking);
        public Task<IReadOnlyList<BookingSummaryDto>> ListByUserAsync(int userId, BookingStatus? status, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<BookingSummaryDto>>([]);
    }

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }
}
