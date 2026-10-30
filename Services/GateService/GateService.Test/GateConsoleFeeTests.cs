using GateService.Application.Features;
using GateService.Application.Features.GateConsole;
using GateService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Test;

/// <summary>
/// US-044 (Check-in Grace Period 15'), UC-22/US-045→047 (Check-out &amp; chốt phí), UC-20 (danh sách booking trong ca).
/// Unit test với fake port — không cần database.
/// </summary>
public class GateConsoleFeeTests
{
    // Thứ Sáu 02/10/2026 09:00 UTC — ngày thường.
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
    private static GateConsoleOptions Options => new();

    private static CheckInUseCase NewCheckIn(FakeRecognizer recognizer, FakeWriter writer, FakeGateEvents events,
        FakeBookings bookings, DateTimeOffset? at = null, GateConsoleOptions? options = null)
    {
        var clock = new FixedTimeProvider(at ?? Now);
        return new CheckInUseCase(recognizer, new BookingQrTokenValidator(clock), bookings, writer, events,
            clock, options ?? Options);
    }

    // ===== US-044: Check-in Grace Period 15 phút =====

    [Fact]
    public async Task CheckIn_late_12_minutes_is_within_grace_and_allowed()
    {
        // US-044 AC1: đến trễ 12 phút → booking vẫn Active, check-in bình thường.
        var bookings = new FakeBookings().WithSnapshot(new GateBookingSnapshot("BK-0002", "Confirmed", 2,
            "51F-123.45", "Sedan", Now.AddMinutes(-12).UtcDateTime, Now.AddMinutes(108).UtcDateTime, "B1-A02",
            63000, 15, 1.5m));
        var writer = new FakeWriter();
        var events = new FakeGateEvents();

        var dto = await NewCheckIn(new FakeRecognizer(), writer, events, bookings)
            .ExecuteAsync(new CheckInCommand(2, GateMethod.BookingCode, "51F-123.45", BookingCode: "BK-0002", StaffUserId: 4));

        Assert.Equal(ParkingSessionStatus.Active.ToString(), dto.Status);
        var draft = Assert.Single(events.Drafts);
        Assert.Contains("trong ân hạn", draft.Note);           // Ghi nhận đi trễ nhưng trong ân hạn
        Assert.Contains("12 phút", draft.Note);
        Assert.Equal(["BK-0002"], bookings.CheckInCalls);        // Đồng bộ booking → CheckedIn
    }

    [Fact]
    public async Task CheckIn_late_beyond_grace_is_flagged_but_staff_can_still_admit()
    {
        // Trễ 20 phút (vượt ân hạn 15') → vẫn cho vào khi staff xác nhận, nhưng phải ghi cảnh báo đối soát.
        var bookings = new FakeBookings().WithSnapshot(new GateBookingSnapshot("BK-0002", "Confirmed", 2,
            "51F-123.45", "Sedan", Now.AddMinutes(-20).UtcDateTime, Now.AddMinutes(100).UtcDateTime, "B1-A02",
            63000, 15, 1.5m));
        var events = new FakeGateEvents();

        var dto = await NewCheckIn(new FakeRecognizer(), new FakeWriter(), events, bookings)
            .ExecuteAsync(new CheckInCommand(2, GateMethod.BookingCode, "51F-123.45", BookingCode: "BK-0002", StaffUserId: 4));

        Assert.Equal(ParkingSessionStatus.Active.ToString(), dto.Status);
        Assert.Contains("VƯỢT ân hạn", Assert.Single(events.Drafts).Note);
    }

    [Fact]
    public async Task CheckIn_rejects_booking_that_is_not_confirmed()
    {
        // UC-21 E1: booking Pending/Cancelled/Expired → từ chối cho vào kèm lý do.
        var bookings = new FakeBookings().WithSnapshot(new GateBookingSnapshot("BK-0001", "Cancelled", 2,
            "51F-123.45", "Sedan", Now.AddHours(-2).UtcDateTime, Now.UtcDateTime, null, 50000, 15, 1.5m));
        var writer = new FakeWriter();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => NewCheckIn(new FakeRecognizer(), writer,
            new FakeGateEvents(), bookings).ExecuteAsync(
            new CheckInCommand(2, GateMethod.BookingCode, "51F-123.45", BookingCode: "BK-0001")));

        Assert.Contains("Cancelled", ex.Message);
        Assert.Empty(writer.Sessions);          // Không tạo lượt gửi
        Assert.Empty(bookings.CheckInCalls);    // Không đồng bộ booking
    }

    [Fact]
    public async Task CheckIn_second_time_for_same_booking_conflicts_even_if_booking_still_confirmed()
    {
        // T-704 + hợp đồng TV7: 1 booking chỉ sinh đúng 1 lượt gửi — kể cả khi booking chưa kịp sync
        // (sync lỗi mạng) thì thao tác check-in lặp vẫn bị chặn 409, không bao giờ tạo lượt thứ hai.
        var bookings = new FakeBookings();
        var writer = new FakeWriter();
        var existing = new ParkingSession
        {
            Code = "PS-EXIST", ParkingLotId = 2, BookingCode = "BK-0002", PlateNumber = "51F12345",
            EntryAtUtc = Now.AddMinutes(-3).UtcDateTime, CheckInMethod = GateMethod.BookingCode,
            Status = ParkingSessionStatus.Active,
        };
        writer.Sessions.Add(existing);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => NewCheckIn(new FakeRecognizer(), writer,
            new FakeGateEvents(), bookings).ExecuteAsync(
            new CheckInCommand(2, GateMethod.BookingCode, "51F-123.45", BookingCode: "BK-0002", StaffUserId: 4)));

        Assert.Contains("đã được check-in trước đó", ex.Message);
        Assert.Single(writer.Sessions);                 // Không tạo lượt mới
        Assert.Empty(bookings.CheckInCalls);            // Chặn trước khi đồng bộ booking
    }

    // ===== UC-22 / US-045 → 047: Check-out & chốt phí =====

    [Fact]
    public async Task CheckOut_confirmFee_sets_fee_records_settlement_and_completes_booking()
    {
        var writer = new FakeWriter();
        var events = new FakeGateEvents();
        var bookings = new FakeBookings().WithSnapshot(new GateBookingSnapshot("BK-0002", "CheckedIn", 2,
            "51F-123.45", "Sedan", Now.AddMinutes(-60).UtcDateTime, Now.AddMinutes(-30).UtcDateTime, "B1-A02",
            63000, 15, 1.5m));
        var exitFees = new FakeExitFees();          // Fee 80.000đ (gồm quá giờ 30.000đ)
        var settlements = new FakeSettlements();
        writer.Sessions.Add(new ParkingSession
        {
            Code = "PS-FEE", ParkingLotId = 2, OwnerProfileId = 1, BookingCode = "BK-0002", PlateNumber = "51F12345",
            VehicleType = VehicleType.Sedan, EntryAtUtc = Now.AddMinutes(-60).UtcDateTime,
            CheckInMethod = GateMethod.BookingCode, Status = ParkingSessionStatus.Active,
        });

        var result = await NewCheckOut(writer, events, bookings, exitFees, settlements)
            .ExecuteAsync(new CheckOutCommand(2, "51F-123.45", StaffUserId: 4, ConfirmFee: true));

        // Lượt gửi: đã đóng kèm Fee/OverstayFee (đọc từ entity để thấy cả OverstayFee)
        Assert.Equal(ParkingSessionStatus.Completed.ToString(), result.Session.Status);
        Assert.Equal(80000m, result.Session.Fee);
        var saved = writer.Sessions.Single();
        Assert.Equal(17000m, saved.OverstayFee);                // 80.000 − 63.000 đã trả trước

        // Báo giá trả về staff: tổng, đã trả trước, cần thu thêm
        Assert.NotNull(result.Fee);
        Assert.Equal(80000m, result.Fee!.SessionFee);
        Assert.Equal(63000m, result.Fee.PrepaidAmount);
        Assert.Equal(17000m, result.Fee.AdditionalDue);

        // Đã ghi thu tiền mặt đúng số tiền cần thu
        var record = Assert.Single(settlements.Records);
        Assert.Equal(17000m, record.Amount);
        Assert.Equal("BK-0002", record.BookingCode);
        Assert.Equal("Cash", record.Method);
        Assert.Equal(4, record.ConfirmedByStaffId);

        // Audit note có mã Payment; booking được đồng bộ Completed
        Assert.Contains("PM-TEST-0001", Assert.Single(events.Drafts).Note);
        Assert.Equal(["BK-0002"], bookings.CheckOutCalls);
        Assert.Null(result.Warning);
    }

    [Fact]
    public async Task CheckOut_confirmFee_without_extra_charge_skips_settlement()
    {
        // Đã trả trước nhiều hơn phí lượt → không thu thêm, không ghi Payment.
        var writer = new FakeWriter();
        var bookings = new FakeBookings().WithSnapshot(new GateBookingSnapshot("BK-0002", "CheckedIn", 2,
            "51F-123.45", "Sedan", Now.AddMinutes(-60).UtcDateTime, Now.AddMinutes(-30).UtcDateTime, "B1-A02",
            100000, 15, 1.5m));
        var exitFees = new FakeExitFees();   // Fee 80.000 < prepaid 100.000
        var settlements = new FakeSettlements();
        writer.Sessions.Add(new ParkingSession
        {
            Code = "PS-FEE2", ParkingLotId = 2, BookingCode = "BK-0002", PlateNumber = "51F12345",
            EntryAtUtc = Now.AddMinutes(-60).UtcDateTime, CheckInMethod = GateMethod.BookingCode,
            Status = ParkingSessionStatus.Active,
        });

        var result = await NewCheckOut(writer, new FakeGateEvents(), bookings, exitFees, settlements)
            .ExecuteAsync(new CheckOutCommand(2, "51F-123.45", StaffUserId: 4, ConfirmFee: true));

        // Tổng phí lượt vẫn được chốt (80.000) nhưng đã trả trước nhiều hơn → không thu thêm, không ghi Payment.
        Assert.Equal(80000m, result.Session.Fee);
        Assert.Equal(0m, result.Fee!.AdditionalDue);
        Assert.Equal(0m, writer.Sessions.Single().OverstayFee);
        Assert.Empty(settlements.Records);
    }

    [Fact]
    public async Task CheckOut_fee_calculation_uses_booking_price_lock()
    {
        // Phí phải tính theo snapshot của booking: grace 10', hệ số quá giờ 2.0, loại xe SUV.
        var writer = new FakeWriter();
        var bookings = new FakeBookings().WithSnapshot(new GateBookingSnapshot("BK-0002", "CheckedIn", 2,
            "51F-123.45", "Suv", Now.AddMinutes(-90).UtcDateTime, Now.AddMinutes(-30).UtcDateTime, "B1-A02",
            63000, 10, 2.0m));
        var exitFees = new FakeExitFees();
        writer.Sessions.Add(new ParkingSession
        {
            Code = "PS-FEE3", ParkingLotId = 2, BookingCode = "BK-0002", PlateNumber = "51F12345",
            EntryAtUtc = Now.AddMinutes(-90).UtcDateTime, CheckInMethod = GateMethod.BookingCode,
            Status = ParkingSessionStatus.Active,
        });

        await NewCheckOut(writer, new FakeGateEvents(), bookings, exitFees, new FakeSettlements())
            .ExecuteAsync(new CheckOutCommand(2, "51F-123.45", StaffUserId: 4, ConfirmFee: true));

        var request = exitFees.LastRequest!;
        Assert.Equal("Suv", request.VehicleType);
        Assert.Equal(10, request.BillingGracePeriodMinutes);
        Assert.Equal(2.0m, request.OverstayMultiplier);
        Assert.Equal(Now.AddMinutes(-30).UtcDateTime, request.BookedEndAtUtc);
    }

    [Fact]
    public async Task ExitQuote_returns_breakdown_for_active_session()
    {
        var sessions = new FakeSessionQueries();
        sessions.Sessions.Add(new ParkingSession
        {
            Code = "PS-QUOTE", ParkingLotId = 2, BookingCode = "BK-0002", PlateNumber = "51F12345",
            EntryAtUtc = Now.AddMinutes(-45).UtcDateTime, CheckInMethod = GateMethod.BookingCode,
            Status = ParkingSessionStatus.Active,
        });
        var useCase = new QuoteExitFeeUseCase(sessions, new FakeBookings(), new FakeExitFees(),
            Options, new FixedTimeProvider(Now));

        var quote = await useCase.ExecuteAsync(2, "51F-123.45");

        Assert.Equal("PS-QUOTE", quote.SessionCode);
        Assert.Equal(80000m, quote.SessionFee);
        Assert.Equal(17000m, quote.AdditionalDue);
        Assert.Equal(45, quote.MinutesParked);
    }

    // ===== UC-20: danh sách booking trong ca (GateService gộp booking + khách vãng lai) =====

    [Fact]
    public async Task ShiftBookings_merges_bookings_and_walkins_sorted_by_arrival()
    {
        var shifts = new FakeShiftQueries(new ShiftDto(1, 2, 4, Now.AddHours(-2).UtcDateTime, null, "Open"));
        var bookings = new FakeBookings().WithListItems(
            new GateBookingListItem("BK-0002", "51F12345", "Sedan", "B1-A02",
                Now.AddHours(-1).UtcDateTime, Now.AddHours(2).UtcDateTime, "CheckedIn", false, 63000m),
            new GateBookingListItem("BK-0003", "30A67890", "Sedan", "B1-A03",
                Now.AddMinutes(-10).UtcDateTime, Now.AddMinutes(110).UtcDateTime, "Confirmed", true, 50000m));
        var sessions = new FakeSessionQueries();
        sessions.Sessions.Add(new ParkingSession
        {
            Code = "PS-WALK", ParkingLotId = 2, PlateNumber = "51A99999", IsWalkIn = true,
            EntryAtUtc = Now.AddMinutes(-30).UtcDateTime, CheckInMethod = GateMethod.Ocr,
            Status = ParkingSessionStatus.Active,
        });
        var useCase = new ListShiftBookingsUseCase(shifts, bookings, sessions, new FixedTimeProvider(Now));

        var items = await useCase.ExecuteAsync(new ShiftBookingsQuery(null, 4, null, null, null));

        // Sắp theo giờ đến: BK-0002 (−1h) → vãng lai (−30') → BK-0003 quá giờ (−10')
        Assert.Collection(items,
            i => { Assert.Equal("BK-0002", i.Code); Assert.Equal("Booking", i.Source); },
            i => { Assert.Equal("PS-WALK", i.Code); Assert.Equal("WalkIn", i.Source); },
            i => { Assert.Equal("BK-0003", i.Code); Assert.True(i.IsOverdue); });
    }

    [Fact]
    public async Task ShiftBookings_requires_lot_when_staff_has_no_open_shift()
        => await Assert.ThrowsAsync<ValidationException>(() => new ListShiftBookingsUseCase(
                new FakeShiftQueries(null), new FakeBookings(), new FakeSessionQueries(), new FixedTimeProvider(Now))
            .ExecuteAsync(new ShiftBookingsQuery(null, 4, null, null, null)));

    private static CheckOutUseCase NewCheckOut(FakeWriter writer, FakeGateEvents events, FakeBookings bookings,
        FakeExitFees exitFees, FakeSettlements settlements)
        => new(new FakeRecognizer(), bookings, exitFees, settlements, writer, events, new FixedTimeProvider(Now), Options);

    private sealed class FakeShiftQueries(ShiftDto? openShift) : IShiftQueries
    {
        public Task<ShiftDto?> FindOpenByStaffAsync(int staffUserId, CancellationToken cancellationToken)
            => Task.FromResult(openShift);
    }
}

