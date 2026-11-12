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

    private static BookingDetailDto SampleBooking(string status, decimal paid, DateTime start) => new(
        2, "BK-0002", status, 5, 1, "51F12345", "Sedan", 1, "Bãi xe Vincom Đồng Khởi", 1, 2, "B1-A02",
        start, start.AddHours(3), null, 70000, 7000, paid, "WELCOME10", null, [], "mock-qr");

    private sealed class FakeQueries(BookingDetailDto? booking) : IBookingQueries
    {
        public Task<BookingDetailDto?> GetByCodeAsync(string code, CancellationToken cancellationToken) => Task.FromResult(booking);
        public Task<IReadOnlyList<BookingSummaryDto>> ListByUserAsync(int userId, BookingStatus? status, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<BookingSummaryDto>>([]);
        public Task<IReadOnlyList<GateBookingListItemDto>> ListForGateAsync(int parkingLotId, DateTime fromUtc, DateTime toUtc,
            DateTime nowUtc, int gracePeriodMinutes, string? search, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<GateBookingListItemDto>>([]);
        public Task<IReadOnlyList<ApprovalLogDto>> ListApprovalLogsAsync(
            int? parkingLotId, DateTime? fromUtc, DateTime? toUtc, string? result, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ApprovalLogDto>>([]);
    }

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }
}

/// <summary>
/// Kiểm tra quy tắc chuyển trạng thái trong Domain Entity Booking (Task T-301).
/// </summary>
public class BookingStateMachineTests
{
    private static readonly DateTime BaseTime = new(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MarkAsPendingPayment_sets_hold_time_and_adds_log()
    {
        var booking = new Booking { Code = "BK-001", UserId = 10, Status = BookingStatus.Created };
        booking.MarkAsPendingPayment(BaseTime);

        Assert.Equal(BookingStatus.PendingPayment, booking.Status);
        Assert.Equal(BaseTime.AddMinutes(15), booking.HoldExpiresAtUtc);
        Assert.Single(booking.StatusLogs);
        Assert.Equal(BookingStatus.PendingPayment, booking.StatusLogs.First().ToStatus);
    }

    [Fact]
    public void MarkAsPendingPayment_from_invalid_status_throws_exception()
    {
        var booking = new Booking { Code = "BK-001", Status = BookingStatus.Confirmed };
        Assert.Throws<ConflictException>(() => booking.MarkAsPendingPayment(BaseTime));
    }

    [Fact]
    public void ConfirmPayment_transitions_to_confirmed_and_records_paid_amount()
    {
        var booking = new Booking { Code = "BK-001", Status = BookingStatus.PendingPayment };
        booking.ConfirmPayment(50000, BaseTime);

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(50000, booking.PaidAmount);
        Assert.Equal(BaseTime, booking.ConfirmedAtUtc);
    }

    [Fact]
    public void CheckIn_and_CheckOut_transitions_work_correctly()
    {
        var booking = new Booking { Code = "BK-001", Status = BookingStatus.Confirmed };
        var inTime = BaseTime.AddHours(1);
        booking.CheckIn(inTime, staffUserId: 99, gateDeviceId: 1);

        Assert.Equal(BookingStatus.CheckedIn, booking.Status);
        Assert.Equal(inTime, booking.CheckedInAtUtc);

        var outTime = inTime.AddHours(2);
        booking.CheckOut(outTime, staffUserId: 99, gateDeviceId: 2);

        Assert.Equal(BookingStatus.Completed, booking.Status);
        Assert.Equal(outTime, booking.CheckedOutAtUtc);
    }

    [Fact]
    public void CheckIn_when_not_confirmed_throws_exception()
    {
        var booking = new Booking { Code = "BK-001", Status = BookingStatus.PendingPayment };
        Assert.Throws<ConflictException>(() => booking.CheckIn(BaseTime));
    }

    [Fact]
    public void Expire_transitions_to_expired_when_hold_time_has_passed()
    {
        var booking = new Booking
        {
            Code = "BK-001",
            Status = BookingStatus.PendingPayment,
            HoldExpiresAtUtc = BaseTime.AddMinutes(15)
        };

        // Vẫn còn trong 15 phút -> ném lỗi
        Assert.Throws<ValidationException>(() => booking.Expire(BaseTime.AddMinutes(10)));

        // Đã qua 15 phút -> thành công
        booking.Expire(BaseTime.AddMinutes(16));
        Assert.Equal(BookingStatus.Expired, booking.Status);
    }

    [Fact]
    public void CancelByCustomer_when_already_checked_in_throws_exception()
    {
        var booking = new Booking { Code = "BK-001", Status = BookingStatus.CheckedIn };
        Assert.Throws<ConflictException>(() =>
            booking.CancelByCustomer(BookingStatus.Cancelled, BaseTime, cancelledByUserId: 10, reason: "Khách bận"));
    }

    [Fact]
    public void CancelByCustomer_from_terminal_status_throws_conflict_exception()
    {
        var booking = new Booking { Code = "BK-001", Status = BookingStatus.Cancelled };
        Assert.Throws<ConflictException>(() =>
            booking.CancelByCustomer(BookingStatus.Cancelled, BaseTime, cancelledByUserId: 10, reason: "Hủy tiếp"));
    }

    [Fact]
    public void MarkAsNoShow_throws_when_under_30_minutes_and_succeeds_when_over_30_minutes()
    {
        var booking = new Booking
        {
            Code = "BK-001",
            Status = BookingStatus.Confirmed,
            StartAtUtc = BaseTime
        };

        // Chưa tới 30 phút sau giờ hẹn -> ném lỗi
        Assert.Throws<ValidationException>(() => booking.MarkAsNoShow(BaseTime.AddMinutes(20)));

        // Sau 30 phút -> chuyển NoShow
        booking.MarkAsNoShow(BaseTime.AddMinutes(31), staffUserId: 1);
        Assert.Equal(BookingStatus.NoShow, booking.Status);
    }

    [Fact]
    public void RequireOwnerApproval_and_RejectByOwner_work_correctly()
    {
        var booking = new Booking { Code = "BK-001", Status = BookingStatus.PendingPayment };
        booking.RequireOwnerApproval();

        Assert.Equal(BookingStatus.PendingOwnerApproval, booking.Status);
        Assert.True(booking.RequiresOwnerApproval);

        booking.RejectByOwner(BaseTime, ownerUserId: 88, reason: "Bãi đầy xe đột xuất");
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    [Fact]
    public void ApproveByOwner_transitions_to_confirmed_without_modifying_paid_amount()
    {
        var booking = new Booking
        {
            Code = "BK-001",
            Status = BookingStatus.PendingOwnerApproval,
            TotalAmount = 100000,
            PaidAmount = 0
        };

        booking.ApproveByOwner(BaseTime, ownerUserId: 99, reason: "Chủ bãi đồng ý duyệt");

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(0, booking.PaidAmount); // Không thay đổi PaidAmount
        Assert.Equal(BaseTime, booking.ConfirmedAtUtc);
    }
}

/// <summary>
/// Kiểm tra nghiệp vụ tạo đặt chỗ, xác thực thời gian và sinh/xác thực mã QR token HMAC-SHA256.
/// </summary>
public class BookingFeaturesTests
{
    private static readonly DateTime FixedNow = new(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);

    static BookingFeaturesTests()
    {
        Environment.SetEnvironmentVariable("QRTOKEN_SECRET", "test-secret-key-for-unit-tests-12345");
    }

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
        var useCase = NewCreateBookingUseCase(repo, qr, clock);

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
        var useCase = NewCreateBookingUseCase(repo, qr, clock);

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

    [Fact]
    public async Task CreateBooking_with_lead_time_under_15_minutes_throws_validation_exception()
    {
        var repo = new FakeBookingRepo();
        var qr = new QrTokenService();
        var clock = new FixedClock(FixedNow);
        var useCase = NewCreateBookingUseCase(repo, qr, clock);

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
            StartAtUtc: FixedNow.AddMinutes(10), // < 15 phút
            EndAtUtc: FixedNow.AddHours(2),
            TotalAmount: 60000,
            PromotionCode: null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(cmd));
        Assert.Contains("15 phút", ex.Message);
    }

    [Fact]
    public async Task CreateBooking_with_lead_time_over_30_days_throws_validation_exception()
    {
        var repo = new FakeBookingRepo();
        var qr = new QrTokenService();
        var clock = new FixedClock(FixedNow);
        var useCase = NewCreateBookingUseCase(repo, qr, clock);

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
            StartAtUtc: FixedNow.AddDays(31), // > 30 ngày
            EndAtUtc: FixedNow.AddDays(31).AddHours(2),
            TotalAmount: 60000,
            PromotionCode: null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(cmd));
        Assert.Contains("30 ngày", ex.Message);
    }

    [Fact]
    public async Task CreateBooking_when_slot_already_occupied_throws_conflict_exception()
    {
        // Fix bug mục 1 & ảnh chat: Người A đặt chỗ X rồi, người tiếp theo KHÔNG ĐƯỢC đặt chỗ X đó
        var existingBooking = new Booking
        {

            Code = "BK-EXISTING-01",
            UserId = 99,
            VehicleId = 10,
            PlateNumber = "51A-111.11",
            ParkingLotId = 1,
            SlotId = 2,
            SlotCode = "B1-A02",
            Status = BookingStatus.Confirmed,
            StartAtUtc = FixedNow.AddHours(1),
            EndAtUtc = FixedNow.AddHours(3)
        };

        var repo = new FakeBookingRepo();
        repo.SavedBookings.Add(existingBooking);
        var qr = new QrTokenService();
        var clock = new FixedClock(FixedNow);
        var useCase = NewCreateBookingUseCase(repo, qr, clock);

        // Khách hàng khác (UserId = 5) cố tình đặt cùng SlotId = 2 trong khung giờ giao nhau
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
            StartAtUtc: FixedNow.AddHours(2), // Giao với 1h-3h
            EndAtUtc: FixedNow.AddHours(4),
            TotalAmount: 60000,
            PromotionCode: null);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(cmd));
        Assert.Contains("đã có người đặt trước", ex.Message);
    }

    [Fact]
    public async Task CreateBooking_allows_user_to_book_different_slots_in_same_window()
    {
        // 1 user có thể đặt 2 ô đỗ khác nhau cho 2 xe khác nhau trong cùng khung giờ
        var userExistingBooking = new Booking
        {
            Code = "BK-USER-01",
            UserId = 5,
            VehicleId = 1,
            PlateNumber = "51A-111.11",
            ParkingLotId = 1,
            SlotId = 1,
            SlotCode = "B1-A01",
            Status = BookingStatus.PendingPayment,
            StartAtUtc = FixedNow.AddHours(1),
            EndAtUtc = FixedNow.AddHours(3)
        };

        var repo = new FakeBookingRepo();
        repo.SavedBookings.Add(userExistingBooking);
        var qr = new QrTokenService();
        var clock = new FixedClock(FixedNow);
        var useCase = NewCreateBookingUseCase(repo, qr, clock);

        // Chính user đó (UserId = 5) tạo thêm đơn ở ô khác (SlotId = 99)
        var cmd = new CreateBookingCommand(
            UserId: 5,
            VehicleId: 2,
            PlateNumber: "51F-999.99",
            VehicleType: VehicleType.Sedan,
            ParkingLotId: 1,
            OwnerProfileId: 1,
            ParkingLotName: "Bãi xe Vincom Đồng Khởi",
            ZoneId: 1,
            SlotId: 99,
            SlotCode: "B1-A99",
            AllocationMode: AllocationMode.Dynamic,
            StartAtUtc: FixedNow.AddHours(1).AddMinutes(30),
            EndAtUtc: FixedNow.AddHours(2).AddMinutes(30),
            TotalAmount: 60000,
            PromotionCode: null);

        var result = await useCase.ExecuteAsync(cmd);
        Assert.NotNull(result);
        Assert.Equal(5, result.UserId);
        Assert.Equal("51F-999.99", result.PlateNumber);
    }

    [Fact]
    public async Task ModifyBooking_under_60_minutes_before_start_throws_validation_exception()
    {
        var booking = new Booking
        {
            Code = "BK-0001",
            UserId = 5,
            Status = BookingStatus.Confirmed,
            StartAtUtc = FixedNow.AddMinutes(45), // Chỉ còn 45 phút tới giờ bắt đầu
            EndAtUtc = FixedNow.AddHours(2)
        };

        var repo = new FakeBookingRepo();
        repo.SavedBookings.Add(booking);
        var queries = new FakeQueries(booking);
        var clock = new FixedClock(FixedNow);
        var qr = new QrTokenService();
        var useCase = new ModifyBookingUseCase(repo, queries, qr, clock);

        var cmd = new ModifyBookingCommand(
            UserId: 5,
            NewVehicleId: null,
            NewPlateNumber: null,
            NewStartAtUtc: FixedNow.AddMinutes(50),
            NewEndAtUtc: FixedNow.AddHours(3));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync("BK-0001", cmd));
        Assert.Contains("60 phút", ex.Message);
    }

    [Fact]
    public async Task CreateBooking_with_PayAtGate_sets_hold_time_until_start_plus_15m()
    {
        var repo = new FakeBookingRepo();
        var qr = new QrTokenService();
        var clock = new FixedClock(FixedNow);
        var useCase = NewCreateBookingUseCase(repo, qr, clock);

        var startAt = FixedNow.AddHours(2); // Đặt trước 2 tiếng
        var cmd = new CreateBookingCommand(
            UserId: 5, VehicleId: 1, PlateNumber: "51F-123.45", VehicleType: VehicleType.Sedan,
            ParkingLotId: 1, OwnerProfileId: 1, ParkingLotName: "Bãi Vincom", ZoneId: 1, SlotId: 2, SlotCode: "A-02",
            AllocationMode: AllocationMode.Dynamic, StartAtUtc: startAt, EndAtUtc: startAt.AddHours(2), TotalAmount: 60000,
            PromotionCode: null, RequiresOwnerApproval: false, PayAtGate: true);

        var result = await useCase.ExecuteAsync(cmd);

        Assert.NotNull(result);
        Assert.Equal(startAt.AddMinutes(15), result.HoldExpiresAtUtc); // Giữ đến giờ hẹn + 15p
    }

    [Fact]
    public async Task CreateBooking_when_slot_is_dedicated_to_active_monthly_pass_throws_conflict_exception()
    {
        var repo = new FakeBookingRepo();
        repo.ActiveMonthlyPassSlots.Add(10); // Slot 10 là của khách vé tháng
        var qr = new QrTokenService();
        var clock = new FixedClock(FixedNow);
        var useCase = NewCreateBookingUseCase(repo, qr, clock);

        var cmd = new CreateBookingCommand(
            UserId: 5, VehicleId: 1, PlateNumber: "51F-123.45", VehicleType: VehicleType.Sedan,
            ParkingLotId: 1, OwnerProfileId: 1, ParkingLotName: "Bãi Vincom", ZoneId: 1, SlotId: 10, SlotCode: "VIP-10",
            AllocationMode: AllocationMode.Dynamic, StartAtUtc: FixedNow.AddHours(1), EndAtUtc: FixedNow.AddHours(3), TotalAmount: 60000,
            PromotionCode: null);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(cmd));
        Assert.Contains("vé tháng", ex.Message);
    }

    [Fact]
    public async Task CreateBooking_when_user_cancels_exceeds_abuse_threshold_throws_conflict_exception()
    {
        var repo = new FakeBookingRepo();
        var qr = new QrTokenService();
        var clock = new FixedClock(FixedNow);
        var abuseGuard = new BookingAbuseGuard(new NoAbuseQueries(cancelled: 3), clock, new BookingAbuseOptions { MaxBookingActionsPerHour = 3 });
        var useCase = new CreateBookingUseCase(repo, qr, abuseGuard, new NoPmsLotIntegration(), clock);

        var cmd = new CreateBookingCommand(
            UserId: 5, VehicleId: 1, PlateNumber: "51F-123.45", VehicleType: VehicleType.Sedan,
            ParkingLotId: 1, OwnerProfileId: 1, ParkingLotName: "Bãi Vincom", ZoneId: 1, SlotId: 2, SlotCode: "A-02",
            AllocationMode: AllocationMode.Dynamic, StartAtUtc: FixedNow.AddHours(1), EndAtUtc: FixedNow.AddHours(3), TotalAmount: 60000,
            PromotionCode: null);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(cmd));
        Assert.Contains("tạm thời bị chặn đặt/hủy", ex.Message);
    }

    [Fact]
    public async Task CancelBooking_adds_BookingCancelled_event_to_outbox()
    {
        var booking = new Booking
        {
            Code = "BK-CANCEL-01",
            UserId = 5,
            PaidAmount = 50000,
            Status = BookingStatus.Confirmed,
            StartAtUtc = FixedNow.AddHours(2), // Hủy sớm > 60p -> hoàn 100%
            EndAtUtc = FixedNow.AddHours(4)
        };

        var repo = new FakeBookingRepo();
        repo.SavedBookings.Add(booking);
        var clock = new FixedClock(FixedNow);
        var useCase = NewCancelBookingUseCase(repo, clock);

        var result = await useCase.ExecuteAsync(booking.Code, new CancelBookingCommand(5, "Đổi lịch"));

        Assert.Equal(50000, result.RefundAmount);
        Assert.Single(repo.OutboxMessages);
        Assert.Equal("BookingCancelled", repo.OutboxMessages[0].EventType);
        Assert.Contains("50000", repo.OutboxMessages[0].PayloadJson);
    }

    [Fact]
    public async Task GateActions_CheckIn_too_early_throws_validation_exception()
    {
        var booking = new Booking
        {
            Code = "BK-GATE-01",
            UserId = 5,
            Status = BookingStatus.Confirmed,
            StartAtUtc = FixedNow.AddHours(1), // Hẹn lúc 09:00
            EndAtUtc = FixedNow.AddHours(3)
        };

        var repo = new FakeBookingRepo();
        repo.SavedBookings.Add(booking);
        var queries = new FakeQueries(booking);
        var clock = new FixedClock(FixedNow); // Hiện tại 08:00 (đến sớm 60 phút > 15 phút)
        var useCase = new GateBookingActionsUseCase(repo, queries, clock);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.CheckInAsync(booking.Code, new GateActionCommand()));
        Assert.Contains("Chưa đến khung giờ check-in", ex.Message);
    }

    [Fact]
    public async Task GateActions_CheckIn_after_end_time_throws_validation_exception()
    {
        var booking = new Booking
        {
            Code = "BK-GATE-02",
            UserId = 5,
            Status = BookingStatus.Confirmed,
            StartAtUtc = FixedNow.AddHours(-3),
            EndAtUtc = FixedNow.AddHours(-1) // Hết hạn lúc FixedNow - 1h
        };

        var repo = new FakeBookingRepo();
        repo.SavedBookings.Add(booking);
        var queries = new FakeQueries(booking);
        var clock = new FixedClock(FixedNow);
        var useCase = new GateBookingActionsUseCase(repo, queries, clock);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.CheckInAsync(booking.Code, new GateActionCommand()));
        Assert.Contains("quá hạn", ex.Message);
    }

    private sealed class FakeQueries(Booking? entity) : IBookingQueries
    {
        public Task<BookingDetailDto?> GetByCodeAsync(string code, CancellationToken cancellationToken)
        {
            if (entity == null || entity.Code != code) return Task.FromResult<BookingDetailDto?>(null);
            return Task.FromResult<BookingDetailDto?>(new BookingDetailDto(
                entity.Id, entity.Code, entity.Status.ToString(), entity.UserId, entity.VehicleId,
                entity.PlateNumber, entity.VehicleType.ToString(), entity.ParkingLotId, entity.ParkingLotName,
                entity.ZoneId, entity.SlotId, entity.SlotCode, entity.StartAtUtc, entity.EndAtUtc,
                entity.HoldExpiresAtUtc, entity.TotalAmount, entity.DiscountAmount, entity.PaidAmount,
                entity.PromotionCode, null, [], entity.QrToken));
        }

        public Task<IReadOnlyList<BookingSummaryDto>> ListByUserAsync(int userId, BookingStatus? status, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<BookingSummaryDto>>([]);

        public Task<IReadOnlyList<GateBookingListItemDto>> ListForGateAsync(int parkingLotId, DateTime fromUtc, DateTime toUtc,
            DateTime nowUtc, int gracePeriodMinutes, string? search, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<GateBookingListItemDto>>([]);
        public Task<IReadOnlyList<ApprovalLogDto>> ListApprovalLogsAsync(
            int? parkingLotId, DateTime? fromUtc, DateTime? toUtc, string? result, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ApprovalLogDto>>([]);
    }

    /// <summary>CreateBookingUseCase với guard chống lạm dụng luôn cho phép (US-103) và PMS không cấu hình (US-052).</summary>
    private static CreateBookingUseCase NewCreateBookingUseCase(IBookingRepository repo, IQrTokenService qr, TimeProvider clock)
        => new(repo, qr, new BookingAbuseGuard(new NoAbuseQueries(), clock), new NoPmsLotIntegration(), clock);

    private static CancelBookingUseCase NewCancelBookingUseCase(IBookingRepository repo, TimeProvider clock)
        => new(repo, new BookingAbuseGuard(new NoAbuseQueries(), clock), clock);

    private sealed class NoAbuseQueries(int created = 0, int cancelled = 0) : IBookingAbuseQueries
    {
        public Task<int> CountCreatedSinceAsync(int userId, DateTime sinceUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(created);
        public Task<int> CountCancellationsSinceAsync(int userId, DateTime sinceUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(cancelled);
        public Task<NoShowStatistics> GetNoShowStatisticsAsync(int userId, DateTime sinceUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(new NoShowStatistics(0, 0));
    }

    private sealed class NoPmsLotIntegration : ILotIntegrationClient
    {
        public Task<bool> PushBookingCreatedAsync(PmsBookingPush booking, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FakeBookingRepo : IBookingRepository
    {
        public List<Booking> SavedBookings { get; } = [];
        public List<int> ActiveMonthlyPassSlots { get; } = [];
        public List<(string EventType, string PayloadJson)> OutboxMessages { get; } = [];

        public Task<bool> IsSlotAssignedToActiveMonthlyPassAsync(int slotId, CancellationToken cancellationToken = default)
            => Task.FromResult(ActiveMonthlyPassSlots.Contains(slotId));

        public Task<int> CountCancellationsTodayAsync(int userId, DateTime todayUtc, CancellationToken cancellationToken = default)
        {
            var count = SavedBookings.Count(b =>
                b.UserId == userId &&
                (b.Status == BookingStatus.Cancelled || b.Status == BookingStatus.CancelledNoRefund) &&
                b.CancelledAtUtc >= todayUtc);
            return Task.FromResult(count);
        }

        public Task AddOutboxMessageAsync(string eventType, string payloadJson, CancellationToken cancellationToken = default)
        {
            OutboxMessages.Add((eventType, payloadJson));
            return Task.CompletedTask;
        }

        public Task<Booking?> GetEntityByCodeAsync(string code, CancellationToken cancellationToken = default)
            => Task.FromResult(SavedBookings.FirstOrDefault(b => b.Code == code));

        public Task<Booking?> GetEntityByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(SavedBookings.FirstOrDefault(b => b.Id == id));


        public Task<bool> HasOverlappingBookingForSlotAsync(int slotId, DateTime startUtc, DateTime endUtc, int? excludeBookingId = null, CancellationToken cancellationToken = default)
        {
            var res = SavedBookings.Any(b =>
                b.SlotId == slotId &&
                (excludeBookingId == null || b.Id != excludeBookingId.Value) &&
                b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Expired && b.Status != BookingStatus.Completed && b.Status != BookingStatus.NoShow &&
                b.StartAtUtc < endUtc && startUtc < b.EndAtUtc);
            return Task.FromResult(res);
        }

        public Task<bool> HasOverlappingBookingForUserAsync(int userId, DateTime startUtc, DateTime endUtc, int? excludeBookingId = null, CancellationToken cancellationToken = default)
        {
            var res = SavedBookings.Any(b =>
                b.UserId == userId &&
                (excludeBookingId == null || b.Id != excludeBookingId.Value) &&
                b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Expired && b.Status != BookingStatus.Completed && b.Status != BookingStatus.NoShow &&
                b.StartAtUtc < endUtc && startUtc < b.EndAtUtc);
            return Task.FromResult(res);
        }




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
