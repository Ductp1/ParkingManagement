using Microsoft.Extensions.DependencyInjection;
using ParkingService.Application.Features.LotOperations;
using ParkingService.Domain.Entities;
using ParkingService.Infrastructure.Workers;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace ParkingService.Test;

/// <summary>
/// US-068 (LotOperations – heartbeat &amp; stale data): StalenessEvaluator (pure), entity
/// ParkingLot.RecordHeartbeat/MarkStaleIfOverdue, RecordHeartbeatUseCase và
/// StalenessMonitorWorker.EvaluateAllAsync (fake IServiceScopeFactory) — không DB, không broker.
/// </summary>
public class LotOperationsTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 2, 0, 0, DateTimeKind.Utc);

    private static ParkingLot Lot(int id = 3, int total = 100, int available = 42) => new(
        id, "Bãi Vincom", "72 Lê Thánh Tôn", 10.7781, 106.7019, total, available, 300,
        new TimeOnly(6, 0), new TimeOnly(23, 0), ParkingLotStatus.Active, ownerProfileId: 7);

    private sealed class FakeRepo(List<ParkingLot> lots) : ILotOperationsRepository
    {
        public int SaveCalls { get; private set; }

        public Task<ParkingLot?> GetLotAsync(int lotId, CancellationToken cancellationToken = default)
            => Task.FromResult(lots.FirstOrDefault(l => l.Id == lotId));

        public Task<List<ParkingLot>> GetActiveLotsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(lots.Where(l => l.Status == ParkingLotStatus.Active).ToList());

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEvents : ILotEventPublisher
    {
        public List<IntegrationEvent> Events { get; } = [];
        public void Enqueue(IntegrationEvent integrationEvent) => Events.Add(integrationEvent);
    }

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }

    private static (StalenessMonitorWorker Worker, FakeRepo Repo, RecordingEvents Events) BuildWorker(List<ParkingLot> lots)
    {
        var repo = new FakeRepo(lots);
        var events = new RecordingEvents();
        var services = new ServiceCollection();
        services.AddScoped<ILotOperationsRepository>(_ => repo);
        services.AddScoped<ILotEventPublisher>(_ => events);
        var provider = services.BuildServiceProvider();
        var worker = new StalenessMonitorWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new StalenessOptions { StaleAfterMinutes = 3, PollSeconds = 30 }),
            new FixedClock(Now),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<StalenessMonitorWorker>.Instance);
        return (worker, repo, events);
    }

    // ===== StalenessEvaluator (pure) =====

    [Fact]
    public void Stale_evaluator_marks_lot_without_any_heartbeat()
        => Assert.True(StalenessEvaluator.NeedsTransitionToStale(null, isStale: false, Now, TimeSpan.FromMinutes(3)));

    [Theory]
    [InlineData(2, false)] // 2 phút < 3 phút → fresh
    [InlineData(4, true)]  // 4 phút > 3 phút → overdue
    public void Stale_evaluator_compares_last_heartbeat_against_threshold(int minutesSinceHeartbeat, bool expected)
        => Assert.Equal(expected, StalenessEvaluator.NeedsTransitionToStale(
            Now.AddMinutes(-minutesSinceHeartbeat), isStale: false, Now, TimeSpan.FromMinutes(3)));

    [Fact]
    public void Stale_evaluator_does_not_retransition_when_already_stale()
        => Assert.False(StalenessEvaluator.NeedsTransitionToStale(Now.AddHours(-1), isStale: true, Now, TimeSpan.FromMinutes(3)));

    // ===== ParkingLot heartbeat / stale =====

    [Fact]
    public void Heartbeat_sets_timestamp_and_is_not_a_recovery_when_lot_was_fresh()
    {
        var lot = Lot();
        Assert.False(lot.RecordHeartbeat(Now));
        Assert.Equal(Now, lot.LastHeartbeatAtUtc);
        Assert.False(lot.IsDataStale);
    }

    [Fact]
    public void Heartbeat_recovers_stale_lot()
    {
        var lot = Lot();
        lot.RecordHeartbeat(Now.AddMinutes(-10));
        Assert.True(lot.MarkStaleIfOverdue(Now, TimeSpan.FromMinutes(3))); // chuyển stale
        Assert.True(lot.IsDataStale);

        Assert.True(lot.RecordHeartbeat(Now)); // heartbeat resume → recovered
        Assert.False(lot.IsDataStale);
    }

    [Fact]
    public void Mark_stale_transitions_only_once()
    {
        var lot = Lot();
        lot.RecordHeartbeat(Now.AddMinutes(-10));
        Assert.True(lot.MarkStaleIfOverdue(Now, TimeSpan.FromMinutes(3)));
        Assert.False(lot.MarkStaleIfOverdue(Now.AddMinutes(1), TimeSpan.FromMinutes(3))); // đã stale → no-op
    }

    [Fact]
    public void Mark_stale_skips_fresh_lot()
    {
        var lot = Lot();
        lot.RecordHeartbeat(Now.AddSeconds(-30));
        Assert.False(lot.MarkStaleIfOverdue(Now, TimeSpan.FromMinutes(3)));
        Assert.False(lot.IsDataStale);
    }


    // ===== RecordHeartbeatUseCase =====

    [Fact]
    public async Task Record_heartbeat_use_case_updates_repo_and_reports_recovery()
    {
        var lot = Lot();
        lot.RecordHeartbeat(Now.AddMinutes(-10));
        Assert.True(lot.MarkStaleIfOverdue(Now.AddMinutes(-1), TimeSpan.FromMinutes(3)));
        var repo = new FakeRepo([lot]);

        var result = await new RecordHeartbeatUseCase(repo, new FixedClock(Now)).ExecuteAsync(3);

        Assert.Equal(Now, result.ReceivedAtUtc);
        Assert.True(result.RecoveredFromStale);
        Assert.False(lot.IsDataStale);
        Assert.Equal(1, repo.SaveCalls);
    }

    [Fact]
    public async Task Record_heartbeat_use_case_throws_for_unknown_lot()
    {
        var repo = new FakeRepo([]);
        await Assert.ThrowsAsync<NotFoundException>(
            () => new RecordHeartbeatUseCase(repo, new FixedClock(Now)).ExecuteAsync(99));
    }

    // ===== StalenessMonitorWorker.EvaluateAllAsync =====

    [Fact]
    public async Task Stale_worker_marks_overdue_active_lot_and_publishes_event_once()
    {
        var lot = Lot();
        lot.RecordHeartbeat(Now.AddMinutes(-10));
        var (worker, repo, events) = BuildWorker([lot]);

        var transitions = await worker.EvaluateAllAsync();

        Assert.Equal(1, transitions);
        Assert.True(lot.IsDataStale);
        var evt = Assert.Single(events.Events);
        var stale = Assert.IsType<LotDataMarkedStale>(evt);
        Assert.Equal(3, stale.ParkingLotId);
        Assert.Equal(Now.AddMinutes(-10), stale.LastHeartbeatAtUtc);
        Assert.Equal(1, repo.SaveCalls);
    }

    [Fact]
    public async Task Stale_worker_ignores_fresh_and_non_active_lots()
    {
        var fresh = Lot(1);
        fresh.RecordHeartbeat(Now.AddSeconds(-30));
        var overdueButInactive = Lot(2);
        overdueButInactive.RecordHeartbeat(Now.AddMinutes(-10));
        overdueButInactive.GetType().GetProperty(nameof(ParkingLot.Status))! // Suspended → worker chỉ quét lot Active
            .SetValue(overdueButInactive, ParkingLotStatus.Suspended);
        var (worker, _, events) = BuildWorker([fresh, overdueButInactive]);

        Assert.Equal(0, await worker.EvaluateAllAsync());
        Assert.Empty(events.Events);
        Assert.False(fresh.IsDataStale);
    }
}

