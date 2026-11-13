using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.LotHeartbeat;
using ParkingService.Domain;
using ParkingService.Domain.Entities;
using ParkingService.Infrastructure.Workers;

namespace ParkingService.Test;

/// <summary>
/// US-068: Heartbeat &amp; Stale Data – StalenessEvaluator (pure), ParkingLot.RecordHeartbeat/MarkStale,
/// StalenessMonitorWorker (sweep) và 2 use case RecordLotHeartbeat/GetLotFreshness.
/// </summary>
public class LotHeartbeatTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan DefaultStaleAfter = TimeSpan.FromMinutes(3);

    private static ParkingLot CreateLot(int id = 1, ParkingLotStatus status = ParkingLotStatus.Active)
        => new(
            id: id,
            name: "Bãi xe Test",
            address: "123 Test Street",
            latitude: 10.77,
            longitude: 106.70,
            totalSlots: 20,
            availableSlots: 20,
            maxHeightCm: 220,
            openTime: new TimeOnly(6, 0),
            closeTime: new TimeOnly(22, 0),
            status: status,
            ownerProfileId: 1);

    // ==================== StalenessEvaluator (pure logic) ====================

    [Fact] // US-068: chưa từng nhận heartbeat (null) => stale
    public void IsStale_WhenNeverReceivedHeartbeat_ReturnsTrue()
        => Assert.True(StalenessEvaluator.IsStale(null, DefaultStaleAfter, Now));

    [Fact] // US-068: đúng tại ngưỡng (== StaleAfterMinutes) vẫn còn Fresh
    public void IsStale_ExactlyAtThreshold_ReturnsFalse()
    {
        var last = Now - DefaultStaleAfter;
        Assert.False(StalenessEvaluator.IsStale(last, DefaultStaleAfter, Now));
    }

    [Fact] // US-068: quá ngưỡng => stale
    public void IsStale_PastThreshold_ReturnsTrue()
    {
        var last = Now - DefaultStaleAfter - TimeSpan.FromSeconds(1);
        Assert.True(StalenessEvaluator.IsStale(last, DefaultStaleAfter, Now));
    }

    [Fact] // US-068: trong ngưỡng => fresh
    public void IsStale_WithinThreshold_ReturnsFalse()
    {
        var last = Now - TimeSpan.FromMinutes(1);
        Assert.False(StalenessEvaluator.IsStale(last, DefaultStaleAfter, Now));
    }

    // ==================== ParkingLot.RecordHeartbeat / MarkStale ====================

    [Fact] // US-068: heartbeat lần đầu – không phải recovery, refresh thời điểm
    public void RecordHeartbeat_FirstBeat_RefreshesAndIsNotRecovery()
    {
        var lot = CreateLot();
        var recovered = lot.RecordHeartbeat(Now);
        Assert.False(recovered);
        Assert.Equal(Now, lot.LastHeartbeatAtUtc);
        Assert.False(lot.IsDataStale);
    }

    [Fact] // US-068: worker đánh dấu stale lần đầu => true, lần sau idempotent => false
    public void MarkStale_ReturnsTrueOnlyOnFirstTransition()
    {
        var lot = CreateLot();
        lot.RecordHeartbeat(Now);
        Assert.True(lot.MarkStale());
        Assert.True(lot.IsDataStale);
        Assert.False(lot.MarkStale()); // lượt quét kế tiếp không phát lại sự kiện
    }

    [Fact] // US-068 AC: heartbeat sau khi stale => tự phục hồi + recovery flag
    public void RecordHeartbeat_AfterStale_RecoversWithRecoveryFlag()
    {
        var lot = CreateLot();
        lot.RecordHeartbeat(Now);
        lot.MarkStale();

        var recovered = lot.RecordHeartbeat(Now.AddSeconds(30));

        Assert.True(recovered);
        Assert.False(lot.IsDataStale);
        Assert.Equal(Now.AddSeconds(30), lot.LastHeartbeatAtUtc);
    }

    // ==================== StalenessMonitorWorker (sweep quét bãi Active) ====================

    [Fact] // US-068: worker chỉ mark các bãi Active quá hạn và phát LotDataMarkedStale đúng 1 lần
    public async Task Sweep_MarksOnlyOverdueActiveLots_AndPublishesStaleEventsOnce()
    {
        var freshLot = CreateLot(1);
        freshLot.RecordHeartbeat(Now.AddSeconds(-10));            // còn fresh
        var staleLot = CreateLot(2);
        staleLot.RecordHeartbeat(Now.AddMinutes(-5));             // quá hạn ngưỡng 3 phút
        var neverReported = CreateLot(3);                          // chưa từng heartbeat => stale
        var suspended = CreateLot(4, ParkingLotStatus.Suspended);  // không Active => worker bỏ qua

        var repo = new FakeStalenessRepo([freshLot, staleLot, neverReported, suspended]);
        var worker = new StalenessMonitorWorker(
            new ServiceScopeFactoryStub(repo), new LotHeartbeatSettings(), new FixedClock(Now),
            NullLogger<StalenessMonitorWorker>.Instance);

        await worker.SweepForTest();

        Assert.False(freshLot.IsDataStale);
        Assert.True(staleLot.IsDataStale);
        Assert.True(neverReported.IsDataStale);
        Assert.False(suspended.IsDataStale);

        var saved = Assert.Single(repo.SavedEvents);
        Assert.Equal(2, saved.Count); // staleLot + neverReported (suspended bị bỏ qua)
        var staleEvent = Assert.IsType<LotDataMarkedStale>(saved.Single(e => ((LotDataMarkedStale)e).ParkingLotId == 2));
        Assert.Equal(Now.AddMinutes(-5), staleEvent.LastHeartbeatAtUtc);
    }

    [Fact] // US-068: bãi đã stale từ trước => lượt quét kế không phát thêm event (idempotent)
    public async Task Sweep_AlreadyStaleLot_DoesNotPublishAgain()
    {
        var lot = CreateLot(1);
        lot.RecordHeartbeat(Now.AddMinutes(-10));
        lot.MarkStale(); // stale từ lượt quét trước

        var repo = new FakeStalenessRepo([lot]);
        var worker = new StalenessMonitorWorker(
            new ServiceScopeFactoryStub(repo), new LotHeartbeatSettings(), new FixedClock(Now),
            NullLogger<StalenessMonitorWorker>.Instance);

        await worker.SweepForTest();

        Assert.Empty(repo.SavedEvents);
        Assert.True(lot.IsDataStale);
    }

    // ==================== RecordLotHeartbeatUseCase ====================

    [Fact] // US-068: heartbeat hợp lệ – lưu heartbeat + không phát event (chưa từng stale)
    public async Task RecordHeartbeat_ValidLot_SavesBeatWithoutEvents()
    {
        var lot = CreateLot(1);
        var repo = new FakeStalenessRepo([lot]);
        var useCase = new RecordLotHeartbeatUseCase(
            repo, new LotHeartbeatSettings(), new FixedClock(Now), NullLogger<RecordLotHeartbeatUseCase>.Instance);

        var result = await useCase.ExecuteAsync(1);

        Assert.False(result.IsDataStale);
        Assert.False(result.Recovered);
        Assert.Equal(Now, result.LastHeartbeatAtUtc);
        // Heartbeat vẫn SaveChanges nhưng KHÔNG có outbox event nào khi chưa từng stale
        Assert.DoesNotContain(repo.SavedEvents, batch => batch.Count > 0);
    }

    [Fact] // US-068: heartbeat sau stale => phát LotDataRecovered + trả Recovered = true
    public async Task RecordHeartbeat_AfterStale_PublishesRecoveredEvent()
    {
        var lot = CreateLot(1);
        lot.RecordHeartbeat(Now.AddMinutes(-10));
        lot.MarkStale();
        var repo = new FakeStalenessRepo([lot]);
        var useCase = new RecordLotHeartbeatUseCase(
            repo, new LotHeartbeatSettings(), new FixedClock(Now), NullLogger<RecordLotHeartbeatUseCase>.Instance);

        var result = await useCase.ExecuteAsync(1);

        Assert.True(result.Recovered);
        Assert.False(result.IsDataStale);
        var saved = Assert.Single(repo.SavedEvents);
        var recoveredEvent = Assert.IsType<LotDataRecovered>(Assert.Single(saved));
        Assert.Equal(1, recoveredEvent.ParkingLotId);
        Assert.Equal(1, recoveredEvent.OwnerProfileId);
    }

    [Theory] // US-068: lotId không hợp lệ => ValidationException
    [InlineData(0)]
    [InlineData(-5)]
    public async Task RecordHeartbeat_WithInvalidLotId_ThrowsValidationException(int lotId)
    {
        var useCase = new RecordLotHeartbeatUseCase(
            new FakeStalenessRepo([]), new LotHeartbeatSettings(), new FixedClock(Now),
            NullLogger<RecordLotHeartbeatUseCase>.Instance);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(lotId));
    }

    [Fact] // US-068: bãi không tồn tại => NotFoundException
    public async Task RecordHeartbeat_WithUnknownLot_ThrowsNotFoundException()
    {
        var useCase = new RecordLotHeartbeatUseCase(
            new FakeStalenessRepo([]), new LotHeartbeatSettings(), new FixedClock(Now),
            NullLogger<RecordLotHeartbeatUseCase>.Instance);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(99));
    }

    // ==================== GetLotFreshnessUseCase ====================

    [Fact] // US-068: chưa từng heartbeat => NeverReported (kể cả khi worker chưa kịp mark)
    public async Task GetFreshness_NeverReportedLot_ReturnsNeverReported()
    {
        var lot = CreateLot(1);
        var useCase = new GetLotFreshnessUseCase(
            new FakeSearchRepo([lot]), new LotHeartbeatSettings(), new FixedClock(Now));

        var result = await useCase.ExecuteAsync(1);

        Assert.Equal("NeverReported", result.Status);
        Assert.True(result.IsDataStale);
        Assert.Null(result.LastHeartbeatAtUtc);
        Assert.Equal(3, result.StaleAfterMinutes);
    }

    [Fact] // US-068: heartbeat trong ngưỡng => Fresh
    public async Task GetFreshness_RecentHeartbeat_ReturnsFresh()
    {
        var lot = CreateLot(1);
        lot.RecordHeartbeat(Now.AddSeconds(-30));
        var useCase = new GetLotFreshnessUseCase(
            new FakeSearchRepo([lot]), new LotHeartbeatSettings(), new FixedClock(Now));

        var result = await useCase.ExecuteAsync(1);

        Assert.Equal("Fresh", result.Status);
        Assert.False(result.IsDataStale);
    }

    [Fact] // US-068: quá ngưỡng => Stale (tính theo thời gian thực, dù worker chưa mark)
    public async Task GetFreshness_OverdueHeartbeat_ReturnsStale()
    {
        var lot = CreateLot(1);
        lot.RecordHeartbeat(Now.AddMinutes(-4));
        var useCase = new GetLotFreshnessUseCase(
            new FakeSearchRepo([lot]), new LotHeartbeatSettings(), new FixedClock(Now));

        var result = await useCase.ExecuteAsync(1);

        Assert.Equal("Stale", result.Status);
        Assert.True(result.IsDataStale);
    }

    // ==================== LotHeartbeatSettings ====================

    [Theory] // US-068: ngưỡng ngoài [1..120] phút => lỗi cấu hình lúc khởi động
    [InlineData(0)]
    [InlineData(121)]
    public void Settings_WithStaleAfterOutOfRange_ThrowsValidationException(int staleAfterMinutes)
        => Assert.Throws<ValidationException>(() => new LotHeartbeatSettings(staleAfterMinutes, 30));

    [Theory] // US-068: PollSeconds ngoài [5..3600] => lỗi cấu hình lúc khởi động
    [InlineData(0)]
    [InlineData(4000)]
    public void Settings_WithPollSecondsOutOfRange_ThrowsValidationException(int pollSeconds)
        => Assert.Throws<ValidationException>(() => new LotHeartbeatSettings(3, pollSeconds));

    // ==================== Test doubles ====================

    private sealed class FakeStalenessRepo(List<ParkingLot> lots) : ILotStalenessRepository
    {
        public List<List<IntegrationEvent>> SavedEvents { get; } = [];

        public Task<ParkingLot?> GetByIdForUpdateAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(lots.FirstOrDefault(l => l.Id == id));

        public Task<IReadOnlyList<ParkingLot>> ListActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ParkingLot>>(lots.Where(l => l.Status == ParkingLotStatus.Active).ToList());

        public async Task SaveWithEventsAsync(IReadOnlyList<IntegrationEvent> events, CancellationToken cancellationToken = default)
        {
            SavedEvents.Add(events.ToList());
            await Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>GetLotFreshnessUseCase dùng IParkingLotRepository (AsNoTracking).</summary>
    private sealed class FakeSearchRepo(List<ParkingLot> lots) : IParkingLotRepository
    {
        public Task<ParkingLot?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(lots.FirstOrDefault(l => l.Id == id));

        public Task<IReadOnlyList<ParkingLot>> FindActiveInBoxAsync(double minLat, double maxLat, double minLng, double maxLng,
            int minHeightCm, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ParkingLot>>([]);
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    /// <summary>Stub IServiceScopeFactory trả scope chứa fake repo cho StalenessMonitorWorker.</summary>
    private sealed class ServiceScopeFactoryStub(ILotStalenessRepository repo) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new StubScope(repo);

        private sealed class StubScope(ILotStalenessRepository repo) : IServiceScope
        {
            public IServiceProvider ServiceProvider => new StubProvider(repo);
            public void Dispose() { }
        }

        private sealed class StubProvider(ILotStalenessRepository repo) : IServiceProvider
        {
            public object? GetService(Type serviceType)
                => serviceType == typeof(ILotStalenessRepository) ? repo : null;
        }
    }
}
