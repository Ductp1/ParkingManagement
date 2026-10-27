using GateService.Application.Features;
using GateService.Application.Features.GateConsole;
using GateService.Application.Features.GateEvents;
using GateService.Application.Features.PlateRecognition;
using GateService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Test;

/// <summary>Fake IPlateRecognizer: chuẩn hoá biển số như ManualPlateRecognizer, ghi lại request cuối.</summary>
public sealed class FakeRecognizer : IPlateRecognizer
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

/// <summary>Fake IParkingSessionWriter: lưu lượt gửi trong bộ nhớ (có tracking như entity thật).</summary>
public sealed class FakeWriter : IParkingSessionWriter
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

/// <summary>Fake IGateEventWriter: ghi nháp các bản nháp sự kiện cổng để test đối chứng.</summary>
public sealed class FakeGateEvents : IGateEventWriter
{
    public List<GateEventDraft> Drafts { get; } = [];

    public Task WriteAsync(GateEventDraft draft, CancellationToken cancellationToken = default)
    {
        Drafts.Add(draft);
        return Task.CompletedTask;
    }
}

/// <summary>Đồng hồ cố định cho unit test.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>Fake IGateBookingClient: mặc định tự sinh booking Confirmed (bắt đầu sau 1 tiếng) cho mọi mã.</summary>
public sealed class FakeBookings : IGateBookingClient
{
    private GateBookingSnapshot? _snapshot;
    private readonly Dictionary<string, GateBookingListItem> _listItems = [];

    public List<string> CheckInCalls { get; } = [];
    public List<string> CheckOutCalls { get; } = [];

    /// <summary>Ép buộc snapshot cho mã booking cụ thể (mặc định: Confirmed, bắt đầu sau 1h).</summary>
    public FakeBookings WithSnapshot(GateBookingSnapshot snapshot)
    {
        _snapshot = snapshot;
        return this;
    }

    public FakeBookings WithListItems(params GateBookingListItem[] items)
    {
        foreach (var i in items) _listItems[i.Code] = i;
        return this;
    }

    public Task<GateBookingSnapshot?> FindByCodeAsync(string bookingCode, CancellationToken cancellationToken = default)
    {
        if (_snapshot is not null) return Task.FromResult<GateBookingSnapshot?>(_snapshot);
        return Task.FromResult<GateBookingSnapshot?>(new GateBookingSnapshot(bookingCode, "Confirmed", 1,
            "51F-123.45", "Sedan", DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(3), "B1-A01",
            63000, 15, 1.5m));
    }

    public Task<IReadOnlyList<GateBookingListItem>> ListForLotAsync(int parkingLotId, DateTime fromUtc, DateTime toUtc,
        string? search, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<GateBookingListItem>>(_listItems.Values.ToList());

    public Task SyncCheckInAsync(string bookingCode, int? staffUserId, CancellationToken cancellationToken = default)
    {
        CheckInCalls.Add(bookingCode);
        return Task.CompletedTask;
    }

    public Task SyncCheckOutAsync(string bookingCode, int? staffUserId, CancellationToken cancellationToken = default)
    {
        CheckOutCalls.Add(bookingCode);
        return Task.CompletedTask;
    }
}

/// <summary>Fake IExitFeeCalculator: trả kết quả cấu hình được (mặc định 80.000đ, quá giờ 30').</summary>
public sealed class FakeExitFees : IExitFeeCalculator
{
    public SessionFeeRequest? LastRequest { get; private set; }

    public int RateCardId { get; init; } = 1;
    public bool IsWithinBillingGrace { get; init; }
    public decimal BaseAmount { get; init; } = 50000m;
    public int OverstayMinutes { get; init; } = 30;
    public decimal OverstayAmount { get; init; } = 30000m;
    public decimal FinalAmount { get; init; } = 80000m;

    public Task<SessionFeeResult> CalculateAsync(SessionFeeRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult(new SessionFeeResult(RateCardId, request.EntryAtUtc == default ? 0 :
            (int)Math.Ceiling((request.ExitAtUtc - request.EntryAtUtc).TotalMinutes), IsWithinBillingGrace,
            BaseAmount, 1m, OverstayMinutes, OverstayAmount, FinalAmount, []));
    }
}

/// <summary>Fake IGateSettlementRecorder: ghi nhận yêu cầu thu tiền và trả mã Payment cố định.</summary>
public sealed class FakeSettlements : IGateSettlementRecorder
{
    public List<GateSettlementRequest> Records { get; } = [];

    public Task<GateSettlementResult> RecordAsync(GateSettlementRequest request, CancellationToken cancellationToken = default)
    {
        Records.Add(request);
        return Task.FromResult(new GateSettlementResult("PM-TEST-0001", "Succeeded"));
    }
}

/// <summary>Fake IParkingSessionQueries: phục vụ QuoteExitFeeUseCase (tra xe đang đỗ).</summary>
public sealed class FakeSessionQueries : IParkingSessionQueries
{
    public List<ParkingSession> Sessions { get; } = [];

    public Task<IReadOnlyList<ParkingSessionDto>> ListByLotAsync(int parkingLotId, ParkingSessionStatus? status,
        DateTime nowUtc, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ParkingSessionDto>>(Sessions
            .Where(s => s.ParkingLotId == parkingLotId && s.Status == (status ?? ParkingSessionStatus.Active))
            .Select(s => ParkingSessionMapper.ToDto(s, nowUtc)).ToList());

    public Task<ParkingSessionDto?> FindActiveByPlateAsync(int parkingLotId, string normalizedPlate, DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var s = Sessions.FirstOrDefault(x => x.ParkingLotId == parkingLotId && x.PlateNumber == normalizedPlate
                                          && x.Status == ParkingSessionStatus.Active);
        return Task.FromResult(s is null ? null : ParkingSessionMapper.ToDto(s, nowUtc));
    }
}
