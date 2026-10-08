using BookingService.Application.Features.Bookings;
using BookingService.Domain.Entities;
using BookingService.Domain.Rules;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace BookingService.Test;

/// <summary>
/// TC-BOOK-07 / TC-BOOK-08 (Kế hoạch kiểm thử v3): Kiểm tra chính sách hủy chỗ và hoàn tiền tại mốc 61 phút, 60 phút và 59 phút.
/// </summary>
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
        2, "BK-0002", status, 5, 1, "51F12345", "Sedan", 1, "Bãi xe Vincom Đồng Khởi", 1, 2, "B1-A02",
        start, start.AddHours(3), null, 70000, 7000, paid, "WELCOME10", null, [], "mock-qr");

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

/// <summary>
/// Kiểm tra nghiệp vụ tạo đặt chỗ, xác thực thời gian và sinh/xác thực mã QR token HMAC-SHA256.
/// </summary>
public class BookingFeaturesTests
{
    private static readonly DateTime FixedNow = new(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void QrTokenService_generates_and_validates_token_correctly()
    {
        var qr = new QrTokenService(new FixedClock(FixedNow));
        var expires = FixedNow.AddHours(4);
        var token = qr.GenerateToken("BK-20261005-0001", "51F12345", 1, expires);

        Assert.NotNull(token);
        var valid = qr.ValidateToken(token, out var code, out var plate, out var lotId);
        Assert.True(valid);
        Assert.Equal("BK-20261005-0001", code);
        Assert.Equal("51F12345", plate);
        Assert.Equal(1, lotId);
    }

    [Fact]
    public void QrTokenService_rejects_tampered_token()
    {
        var qr = new QrTokenService(new FixedClock(FixedNow));
        var token = qr.GenerateToken("BK-20261005-0001", "51F12345", 1, FixedNow.AddHours(4));
        var tampered = token + "bad";

        var valid = qr.ValidateToken(tampered, out _, out _, out _);
        Assert.False(valid);
    }

    [Fact]
    public async Task CreateBooking_sets_pending_payment_and_15_minutes_hold()
    {
        var repo = new FakeBookingRepo();
        var qr = new QrTokenService();
        var clock = new FixedClock(FixedNow);
        var useCase = new CreateBookingUseCase(repo, qr, clock);

        var cmd = new CreateBookingCommand(
            UserId: 5,
            VehicleId: 1,
            PlateNumber: "51F-123.45",
            VehicleType: VehicleType.Sedan,
            ParkingLotId: 1,
            OwnerProfileId: 1,
            ParkingLotName: "Bãi xe Vincom Đồng Khởi",
            ZoneId: 1,
            SlotId: 2,
            SlotCode: "B1-A02",
            AllocationMode: AllocationMode.Dynamic,
            StartAtUtc: FixedNow.AddHours(1),
            EndAtUtc: FixedNow.AddHours(3),
            TotalAmount: 60000,
            PromotionCode: null);

        var result = await useCase.ExecuteAsync(cmd);

        Assert.NotNull(result);
        Assert.StartsWith("BK-", result.Code);
        Assert.Equal("PendingPayment", result.Status);
        Assert.Equal(FixedNow.AddMinutes(15), result.HoldExpiresAtUtc);
        Assert.NotNull(result.QrToken);
        Assert.Single(repo.SavedBookings);
    }

    [Fact]
    public async Task CreateBooking_with_invalid_times_throws_validation_exception()
    {
        var repo = new FakeBookingRepo();
        var qr = new QrTokenService();
        var clock = new FixedClock(FixedNow);
        var useCase = new CreateBookingUseCase(repo, qr, clock);

        var cmd = new CreateBookingCommand(
            UserId: 5,
            VehicleId: 1,
            PlateNumber: "51F-123.45",
            VehicleType: VehicleType.Sedan,
            ParkingLotId: 1,
            OwnerProfileId: 1,
            ParkingLotName: "Bãi xe Vincom Đồng Khởi",
            ZoneId: 1,
            SlotId: 2,
            SlotCode: "B1-A02",
            AllocationMode: AllocationMode.Dynamic,
            StartAtUtc: FixedNow.AddHours(3),
            EndAtUtc: FixedNow.AddHours(1), // Không hợp lệ: End < Start
            TotalAmount: 60000,
            PromotionCode: null);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(cmd));
    }

    private sealed class FakeBookingRepo : IBookingRepository
    {
        public List<Booking> SavedBookings { get; } = [];

        public Task<Booking?> GetEntityByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Task.FromResult(SavedBookings.FirstOrDefault(b => b.Code == code));

        public Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
        {
            SavedBookings.Add(booking);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }
}
