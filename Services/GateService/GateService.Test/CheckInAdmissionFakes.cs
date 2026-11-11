using GateService.Application.Features.GateConsole;
using GateService.Application.Features.GateEvents;
using GateService.Application.Features.LotCapacity;
using GateService.Application.Features.PlateRecognition;
using GateService.Domain.Entities;
using GateService.Domain.Enums;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Test;

/// <summary>Fakes dùng chung cho test admission US-066.</summary>
internal static class CheckInAdmissionFakes
{
    public static CheckInUseCase CreateCheckIn(
        FakeAdmissionGate gate, IParkingSessionWriter? writer = null, RecordingCapacityPublisher? publisher = null,
        IGateBookingClient? bookings = null)
        => new(new FakePlateRecognizer(), new BookingQrTokenValidator(), bookings ?? new FakeGateBookings(),
            writer ?? new RecordingSessionWriter(), new RecordingGateEventWriter(), TimeProvider.System,
            new GateConsoleOptions(), gate, publisher ?? new RecordingCapacityPublisher());

    public static CheckOutUseCase CreateCheckOut(FakeAdmissionGate gate, IParkingSessionWriter writer)
        => new(new FakePlateRecognizer(), new FakeGateBookings(), new FakeExitFeeCalculator(), new FakeSettlementRecorder(),
            writer, new RecordingGateEventWriter(), TimeProvider.System, new GateConsoleOptions(),
            gate, new RecordingCapacityPublisher());
}

internal sealed class FakeAdmissionGate(bool admitted, string? reason = null) : ILotAdmissionGate
{
    public List<(int lotId, AdmissionType type)> AdmitCalls { get; } = [];
    public List<(int lotId, AdmissionType type)> ReleaseCalls { get; } = [];

    public Task<(bool Admitted, string? RejectReason)> TryAdmitAsync(
        int parkingLotId, AdmissionType type, CancellationToken cancellationToken = default)
    {
        AdmitCalls.Add((parkingLotId, type));
        return Task.FromResult((admitted, reason));
    }

    public Task ReleaseAsync(int parkingLotId, AdmissionType type, CancellationToken cancellationToken = default)
    {
        ReleaseCalls.Add((parkingLotId, type));
        return Task.CompletedTask;
    }
}

internal sealed class FakePlateRecognizer : IPlateRecognizer
{
    public Task<PlateRecognitionResult> RecognizeAsync(PlateRecognitionRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = ParkingManagement.SharedKernel.Rules.PlateNormalizer.Normalize(request.RawInput);
        return Task.FromResult<PlateRecognitionResult>(new(normalized, normalized, request.Source, null));
    }
}

/// <summary>Booking fake luôn trả snapshot Confirmed — test admission chỉ quan tâm quota, không quan tâm UC-21.</summary>
internal sealed class FakeGateBookings : IGateBookingClient
{
    public Task<GateBookingSnapshot?> FindByCodeAsync(string bookingCode, CancellationToken cancellationToken = default)
        => Task.FromResult<GateBookingSnapshot?>(new GateBookingSnapshot(
            bookingCode, BookingStatus.Confirmed.ToString(), 3, "51F-123.45", "Sedan",
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(3), null, 50000, 15, 1.5m));

    public Task<IReadOnlyList<GateBookingListItem>> ListForLotAsync(int parkingLotId, DateTime fromUtc, DateTime toUtc,
        string? search, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GateBookingListItem>>([]);

    public Task SyncCheckInAsync(string bookingCode, int? staffUserId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SyncCheckOutAsync(string bookingCode, int? staffUserId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class FakeExitFeeCalculator : IExitFeeCalculator
{
    public Task<SessionFeeResult> CalculateAsync(SessionFeeRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new SessionFeeResult(0, 0, true, 0, 1, 0, 0, 0, Array.Empty<FeeLineDto>()));
}

internal sealed class FakeSettlementRecorder : IGateSettlementRecorder
{
    public Task<GateSettlementResult> RecordAsync(GateSettlementRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new GateSettlementResult("PAY-TEST", "Succeeded"));
}

internal sealed class RecordingSessionWriter : IParkingSessionWriter
{
    public List<ParkingSession> Sessions { get; } = [];

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

    public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class ThrowingSessionWriter : IParkingSessionWriter
{
    public Task<ParkingSession> AddAsync(ParkingSession session, CancellationToken cancellationToken)
        => throw new InvalidOperationException("DB down");

    public Task<ParkingSession?> FindActiveTrackedAsync(int parkingLotId, string normalizedPlate, CancellationToken cancellationToken)
        => Task.FromResult<ParkingSession?>(null);

    public Task<ParkingSession?> FindByBookingCodeAsync(string bookingCode, CancellationToken cancellationToken)
        => Task.FromResult<ParkingSession?>(null);

    public Task SaveAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class RecordingGateEventWriter : IGateEventWriter
{
    public List<GateEventDraft> Drafts { get; } = [];

    public Task<GateEventRecord> WriteAsync(GateEventDraft draft, CancellationToken cancellationToken = default)
    {
        Drafts.Add(draft);
        return Task.FromResult(new GateEventRecord(Drafts.Count, draft.ParkingLotId, draft.EventType.ToString(),
            draft.ParkingSessionId, draft.PlateNumber, draft.PerformedByUserId, draft.Note, draft.ShiftId, DateTime.UtcNow));
    }
}

internal sealed class RecordingCapacityPublisher : ILotCapacityRealtimePublisher
{
    public List<int> Calls { get; } = [];

    public Task PublishCapacityChangedAsync(int parkingLotId, CancellationToken cancellationToken = default)
    {
        Calls.Add(parkingLotId);
        return Task.CompletedTask;
    }
}