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
        FakeAdmissionGate gate, IParkingSessionWriter? writer = null, RecordingCapacityPublisher? publisher = null)
        => new(new FakePlateRecognizer(), new BookingQrTokenValidator(), writer ?? new RecordingSessionWriter(),
            new RecordingGateEventWriter(), TimeProvider.System, gate, publisher ?? new RecordingCapacityPublisher());

    public static CheckOutUseCase CreateCheckOut(FakeAdmissionGate gate, IParkingSessionWriter writer)
        => new(new FakePlateRecognizer(), writer, new RecordingGateEventWriter(), TimeProvider.System,
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
        => Task.FromResult<ParkingSession?>(null);

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

    public Task WriteAsync(GateEventDraft draft, CancellationToken cancellationToken = default)
    {
        Drafts.Add(draft);
        return Task.CompletedTask;
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