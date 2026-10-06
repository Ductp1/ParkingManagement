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
        var useCase = new CheckInUseCase(recognizer, writer, new FakeGateEvents(), TimeProvider.System);

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
        var useCase = new CheckInUseCase(new FakeRecognizer(), writer, events, TimeProvider.System);

        await useCase.ExecuteAsync(new CheckInCommand(1, GateMethod.BookingCode, "30A-123.45", BookingCode: "bk-0002", StaffUserId: 9));

        var session = writer.Sessions.Single();
        Assert.Equal("BK-0002", session.BookingCode); // chuẩn hoá hoa
        Assert.False(session.IsWalkIn);
        Assert.Equal(9, session.CheckedInByStaffId);
        var draft = Assert.Single(events.Drafts);
        Assert.Equal(GateEventType.CheckIn, draft.EventType); // mọi thao tác tay phải ghi GateEvents
    }

    [Fact]
    public async Task CheckIn_qr_without_booking_code_is_rejected()
        => await Assert.ThrowsAsync<ValidationException>(() => new CheckInUseCase(
            new FakeRecognizer(), new FakeWriter(), new FakeGateEvents(), TimeProvider.System)
            .ExecuteAsync(new CheckInCommand(1, GateMethod.Qr, "30A-123.45")));

    [Fact]
    public async Task CheckIn_ocr_method_is_not_supported_yet()
        => await Assert.ThrowsAsync<ValidationException>(() => new CheckInUseCase(
            new FakeRecognizer(), new FakeWriter(), new FakeGateEvents(), TimeProvider.System)
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
            new FakeRecognizer(), writer, new FakeGateEvents(), TimeProvider.System)
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
