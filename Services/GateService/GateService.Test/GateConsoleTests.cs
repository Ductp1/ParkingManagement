using GateService.Application.Features.GateConsole;
using GateService.Application.Features.GateEvents;
using GateService.Application.Features.PlateRecognition;
using GateService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Test;

// T-702: Check-in/Check-out của Gate Console (unit test với fake port, không cần database).
public class GateConsoleTests
{
    [Fact]
    public async Task CheckIn_manual_plate_goes_through_plate_recognizer()
    {
        var recognizer = new FakeRecognizer();
        var writer = new FakeWriter();
        var useCase = new CheckInUseCase(recognizer, new BookingQrTokenValidator(), writer, new FakeGateEvents(), TimeProvider.System);

        var dto = await useCase.ExecuteAsync(new CheckInCommand(2, GateMethod.Manual, " 51f-123.45 "));

        // Biển số phải đi qua IPlateRecognizer (điều kiện để Phase 2 thay OCR).
        Assert.NotNull(recognizer.LastRequest);
        Assert.Equal(" 51f-123.45 ", recognizer.LastRequest.RawInput); // use case đưa raw input vào recognizer
        Assert.Equal("51F12345", dto.PlateNumber);
        Assert.Equal("51F-123.45", dto.PlateDisplay);
        Assert.True(dto.IsWalkIn);                    // không có booking code → vãng lai
        Assert.Equal(ParkingSessionStatus.Active.ToString(), dto.Status);
        var session = writer.Sessions.Single();
        Assert.Equal(GateMethod.Manual, session.CheckInMethod);
    }

    [Fact]
    public async Task CheckIn_with_booking_code_is_not_walk_in()
    {
        var writer = new FakeWriter();
        var events = new FakeGateEvents();
        var useCase = new CheckInUseCase(new FakeRecognizer(), new BookingQrTokenValidator(), writer, events, TimeProvider.System);

        await useCase.ExecuteAsync(new CheckInCommand(1, GateMethod.BookingCode, "30A-123.45", BookingCode: "bk-0002", StaffUserId: 9));

        var session = writer.Sessions.Single();
        Assert.Equal("BK-0002", session.BookingCode); // chuẩn hoá hoa
        Assert.False(session.IsWalkIn);
        Assert.Equal(9, session.CheckedInByStaffId);
        var draft = Assert.Single(events.Drafts);
        Assert.Equal(GateEventType.CheckIn, draft.EventType); // mọi thao tác tay phải ghi GateEvents
    }

    [Fact]
    public async Task CheckIn_qr_without_token_is_rejected()
        => await Assert.ThrowsAsync<ValidationException>(() => new CheckInUseCase(
            new FakeRecognizer(), new BookingQrTokenValidator(), new FakeWriter(), new FakeGateEvents(), TimeProvider.System)
            .ExecuteAsync(new CheckInCommand(1, GateMethod.Qr, "30A-123.45")));

    [Fact]
    public async Task CheckIn_qr_with_invalid_token_is_rejected()
    {
        // Token ký bằng secret khác → chữ ký không khớp.
        var token = TestQrTokenFactory.Create("BK-20261002-0001", "51F-123.45", 1, DateTime.UtcNow.AddHours(2),
            secret: "not-the-shared-secret");

        await Assert.ThrowsAsync<ValidationException>(() => new CheckInUseCase(
            new FakeRecognizer(), new BookingQrTokenValidator(), new FakeWriter(), new FakeGateEvents(), TimeProvider.System)
            .ExecuteAsync(new CheckInCommand(1, GateMethod.Qr, "51F-123.45", QrToken: token)));
    }

    [Fact]
    public async Task CheckIn_qr_with_expired_token_is_rejected()
    {
        var now = new DateTimeOffset(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(now);
        var expired = TestQrTokenFactory.Create("BK-20261002-0001", "51F-123.45", 1, now.AddHours(-1).UtcDateTime);

        await Assert.ThrowsAsync<ValidationException>(() => new CheckInUseCase(
            new FakeRecognizer(), new BookingQrTokenValidator(clock), new FakeWriter(), new FakeGateEvents(), clock)
            .ExecuteAsync(new CheckInCommand(1, GateMethod.Qr, "51F-123.45", QrToken: expired)));
    }

    [Fact]
    public async Task CheckIn_qr_with_wrong_lot_token_is_rejected()
    {
        // Token của bãi 2 nhưng check-in tại bãi 1.
        var token = TestQrTokenFactory.Create("BK-20261002-0001", "51F-123.45", 2, DateTime.UtcNow.AddHours(2));

        await Assert.ThrowsAsync<ValidationException>(() => new CheckInUseCase(
            new FakeRecognizer(), new BookingQrTokenValidator(), new FakeWriter(), new FakeGateEvents(), TimeProvider.System)
            .ExecuteAsync(new CheckInCommand(1, GateMethod.Qr, "51F-123.45", QrToken: token)));
    }

    [Fact]
    public async Task CheckIn_qr_with_valid_token_creates_session_from_verified_payload()
    {
        var writer = new FakeWriter();
        var events = new FakeGateEvents();
        var token = TestQrTokenFactory.Create("bk-20261002-0001", "51F-123.45", 2, DateTime.UtcNow.AddHours(2));

        var dto = await new CheckInUseCase(new FakeRecognizer(), new BookingQrTokenValidator(), writer, events, TimeProvider.System)
            .ExecuteAsync(new CheckInCommand(2, GateMethod.Qr, "51F-123.45", QrToken: token));

        // Mã booking PHẢI lấy từ payload đã xác thực (viết hoa chuẩn hoá), không phải từ client.
        Assert.Equal("BK-20261002-0001", dto.BookingCode);
        Assert.False(dto.IsWalkIn);
        Assert.Equal(GateMethod.Qr.ToString(), dto.CheckInMethod);
        var session = writer.Sessions.Single();
        Assert.Equal("BK-20261002-0001", session.BookingCode);
        var draft = Assert.Single(events.Drafts);
        Assert.Equal(GateEventType.CheckIn, draft.EventType);
        Assert.Equal("Check-in theo booking BK-20261002-0001.", draft.Note); // biển khớp → note bình thường
    }

    [Fact]
    public async Task CheckIn_reused_booking_qr_is_rejected()
    {
        var writer = new FakeWriter();
        writer.Sessions.Add(new ParkingSession
        {
            ParkingLotId = 2, PlateNumber = "51F12345", BookingCode = "BK-20261002-0001",
            EntryAtUtc = DateTime.UtcNow.AddHours(-2), CheckInMethod = GateMethod.Qr,
            Status = ParkingSessionStatus.Completed // lượt cũ đã kết thúc vẫn chặn – 1 booking/1 lượt
        });

        var token = TestQrTokenFactory.Create("BK-20261002-0001", "51F-123.45", 2, DateTime.UtcNow.AddHours(2));

        await Assert.ThrowsAsync<ConflictException>(() => new CheckInUseCase(
            new FakeRecognizer(), new BookingQrTokenValidator(), writer, new FakeGateEvents(), TimeProvider.System)
            .ExecuteAsync(new CheckInCommand(2, GateMethod.Qr, "51F-123.45", QrToken: token)));
    }

    [Fact]
    public async Task CheckIn_qr_plate_mismatch_records_audit_note()
    {
        var writer = new FakeWriter();
        var events = new FakeGateEvents();
        // Booking giữ xe 30A-123.45 nhưng xe đến cổng là 51F-123.45 → vẫn cho vào, phải ghi note đối soát.
        var token = TestQrTokenFactory.Create("BK-20261002-0001", "30A-123.45", 2, DateTime.UtcNow.AddHours(2));

        var dto = await new CheckInUseCase(new FakeRecognizer(), new BookingQrTokenValidator(), writer, events, TimeProvider.System)
            .ExecuteAsync(new CheckInCommand(2, GateMethod.Qr, "51F-123.45", QrToken: token));

        Assert.Equal("51F12345", dto.PlateNumber);          // lượt gửi ghi biển số THỰC TẾ
        Assert.Equal("BK-20261002-0001", dto.BookingCode);
        var draft = Assert.Single(events.Drafts);
        Assert.Contains("30A-123.45", draft.Note);           // note audit nêu cả 2 biển số
        Assert.Contains("51F-123.45", draft.Note);
        Assert.Contains("khác", draft.Note);
    }

    [Fact]
    public async Task CheckIn_ocr_method_is_not_supported_yet()
        => await Assert.ThrowsAsync<ValidationException>(() => new CheckInUseCase(
            new FakeRecognizer(), new BookingQrTokenValidator(), new FakeWriter(), new FakeGateEvents(), TimeProvider.System)
            .ExecuteAsync(new CheckInCommand(1, GateMethod.Ocr, "30A-123.45")));

    [Fact]
    public async Task CheckIn_duplicate_active_plate_conflicts()
    {
        var writer = new FakeWriter();
        writer.Sessions.Add(new ParkingSession
        {
            ParkingLotId = 2, PlateNumber = "51F12345", IsWalkIn = true,
            EntryAtUtc = DateTime.UtcNow.AddHours(-1), CheckInMethod = GateMethod.Manual, Status = ParkingSessionStatus.Active
        });

        await Assert.ThrowsAsync<ConflictException>(() => new CheckInUseCase(
            new FakeRecognizer(), new BookingQrTokenValidator(), writer, new FakeGateEvents(), TimeProvider.System)
            .ExecuteAsync(new CheckInCommand(2, GateMethod.Manual, "51F-123.45")));
    }

    [Fact]
    public async Task CheckOut_completes_active_session_and_writes_event()
    {
        var writer = new FakeWriter();
        var events = new FakeGateEvents();
        var now = new DateTimeOffset(2026, 10, 6, 9, 30, 0, TimeSpan.Zero);
        writer.Sessions.Add(new ParkingSession
        {
            ParkingLotId = 2, PlateNumber = "51F12345", IsWalkIn = true,
            EntryAtUtc = now.AddHours(-1).UtcDateTime, CheckInMethod = GateMethod.Manual, Status = ParkingSessionStatus.Active
        });
        var useCase = new CheckOutUseCase(new FakeRecognizer(), writer, events, new FixedTimeProvider(now));

        var dto = await useCase.ExecuteAsync(new CheckOutCommand(2, "51F12345", StaffUserId: 9));

        Assert.Equal(ParkingSessionStatus.Completed.ToString(), dto.Status);
        Assert.Equal(now.UtcDateTime, dto.ExitAtUtc);
        var session = writer.Sessions.Single();
        Assert.Equal(GateMethod.Manual, session.CheckOutMethod);
        Assert.Equal(9, session.CheckedOutByStaffId);
        Assert.Equal(1, writer.SaveCount);
        var draft = Assert.Single(events.Drafts);
        Assert.Equal(GateEventType.CheckOut, draft.EventType);
    }

    [Fact]
    public async Task CheckOut_unknown_plate_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => new CheckOutUseCase(
            new FakeRecognizer(), new FakeWriter(), new FakeGateEvents(), TimeProvider.System)
            .ExecuteAsync(new CheckOutCommand(2, "51F-123.45")));

    private sealed class FakeRecognizer : IPlateRecognizer
    {
        public PlateRecognitionRequest? LastRequest { get; private set; }

        public Task<PlateRecognitionResult> RecognizeAsync(PlateRecognitionRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var normalized = ParkingManagement.SharedKernel.Rules.PlateNormalizer.Normalize(request.RawInput);
            PlateRecognitionResult result = new(normalized, normalized, request.Source, null);
            return Task.FromResult(result);
        }
    }

    private sealed class FakeWriter : IParkingSessionWriter
    {
        public List<ParkingSession> Sessions { get; } = [];
        public int SaveCount { get; private set; }

        public Task<ParkingSession> AddAsync(ParkingSession session, CancellationToken cancellationToken)
        {
            Sessions.Add(session);
            return Task.FromResult(session);
        }

        public Task<ParkingSession?> FindActiveTrackedAsync(int parkingLotId, string normalizedPlate, CancellationToken cancellationToken)
            => Task.FromResult(Sessions.FirstOrDefault(
                s => s.ParkingLotId == parkingLotId && s.PlateNumber == normalizedPlate && s.Status == ParkingSessionStatus.Active));

        public Task<ParkingSession?> FindByBookingCodeAsync(string bookingCode, CancellationToken cancellationToken)
            => Task.FromResult(Sessions.FirstOrDefault(s => s.BookingCode == bookingCode)); // MỌI trạng thái

        public Task SaveAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGateEvents : IGateEventWriter
    {
        public List<GateEventDraft> Drafts { get; } = [];

        public Task WriteAsync(GateEventDraft draft, CancellationToken cancellationToken = default)
        {
            Drafts.Add(draft);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
