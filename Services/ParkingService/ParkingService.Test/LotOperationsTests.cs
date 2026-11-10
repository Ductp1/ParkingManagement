using Microsoft.Extensions.DependencyInjection;
using ParkingService.Application.Features.LotOperations;
using ParkingService.Domain.Entities;
using ParkingService.Infrastructure.Workers;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace ParkingService.Test;

/// <summary>
/// US-067/068/069 (LotOperations): heartbeat &amp; stale transitions (US-068), dashboard overview
/// (US-067), emergency stop + audit + fan-out event (US-069). Fakes in-memory theo pattern
/// LotCapacityConfigTests — không DB, không broker.
/// </summary>
public class LotOperationsTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 2, 0, 0, DateTimeKind.Utc);

    private static ParkingLot Lot(int id = 3, int total = 100, int available = 42) => new(
        id, "Bãi Vincom", "72 Lê Thánh Tôn", 10.7781, 106.7019, total, available, 300,
        new TimeOnly(6, 0), new TimeOnly(23, 0), ParkingLotStatus.Active, ownerProfileId: 7);

    private static EmergencyAffectedBooking Affected(int bookingId, string code, int userId) =>
        new(bookingId, code, userId, "Confirmed", Now.AddHours(2), Now.AddHours(5));

    private sealed class FakeRepo(List<ParkingLot> lots) : ILotOperationsRepository
    {
        public List<EmergencyStopLog> Logs { get; } = [];
        public int SaveCalls { get; private set; }
        public Dictionary<int, Dictionary<string, int>> SlotCounts { get; } = new();

        public Task<ParkingLot?> GetLotAsync(int lotId, CancellationToken cancellationToken = default)
            => Task.FromResult(lots.FirstOrDefault(l => l.Id == lotId));

        public Task<Dictionary<string, int>> CountSlotsByStateAsync(int lotId, CancellationToken cancellationToken = default)
            => Task.FromResult(SlotCounts.TryGetValue(lotId, out var counts) ? counts : []);

        public Task AddEmergencyLogAsync(EmergencyStopLog log, CancellationToken cancellationToken = default)
        {
            Logs.Add(log);
            return Task.CompletedTask;
        }

        public Task<List<EmergencyStopLog>> GetEmergencyLogsAsync(int lotId, int take, CancellationToken cancellationToken = default)
            => Task.FromResult(Logs.Where(l => l.ParkingLotId == lotId).Take(take).ToList());

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

    private sealed class FakeBookings : ILotBookingLookup
    {
        public List<EmergencyAffectedBooking> Affected { get; set; } = [];
        public (int Active, int Upcoming) Counts { get; set; } = (2, 5);
        public bool Down { get; set; }

        public Task<IReadOnlyList<EmergencyAffectedBooking>> GetAffectedBookingsAsync(int lotId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<EmergencyAffectedBooking>>(Down ? [] : Affected);

        public Task<(int Active, int Upcoming)> GetBookingCountsAsync(int lotId, CancellationToken cancellationToken = default)
            => Task.FromResult(Down ? (0, 0) : Counts);
    }

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }

    private static (StalenessMonitorWorker Worker, FakeRepo Repo, RecordingEvents Events) BuildWorker(List<ParkingLot> lots)
    {
        var repo = new FakeRepo(lots);
        var events = new RecordingEvents();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
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


    // ===== US-068: StalenessEvaluator (pure) =====

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

    // ===== US-068: ParkingLot heartbeat / stale =====

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

    // ===== US-068: RecordHeartbeatUseCase =====

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

    // ===== US-068: StalenessMonitorWorker.EvaluateAllAsync =====

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

    // ===== US-067: GetOperationsOverviewUseCase =====

    [Fact]
    public async Task Overview_combines_occupancy_bookings_and_freshness()
    {
        var lot = Lot(total: 100, available: 42);
        lot.RecordHeartbeat(Now.AddSeconds(-30));
        var repo = new FakeRepo([lot]);
        repo.SlotCounts[3] = new Dictionary<string, int> { ["Available"] = 42, ["Occupied"] = 50, ["Reserved"] = 8 };
        var bookings = new FakeBookings { Counts = (2, 5) };

        var dto = await new GetOperationsOverviewUseCase(repo, bookings, new StalenessOptions()).ExecuteAsync(3);

        Assert.Equal("Active", dto.Status);
        Assert.False(dto.EmergencyStopped);
        Assert.Equal(100, dto.TotalSlots);
        Assert.Equal(58, dto.OccupiedSlots);   // total - available
        Assert.Equal(42, dto.AvailableSlots);
        Assert.Equal(2, dto.ActiveBookings);
        Assert.Equal(5, dto.UpcomingBookings);
        Assert.False(dto.IsDataStale);
        Assert.Equal(Now.AddSeconds(-30), dto.LastHeartbeatAtUtc);
        Assert.Equal(3, dto.StaleThresholdMinutes);
        Assert.Contains("\"Occupied\":50", dto.SlotBreakdownJson);
    }

    [Fact]
    public async Task Overview_degrades_to_zero_bookings_when_booking_service_down()
    {
        var lot = Lot();
        var repo = new FakeRepo([lot]);
        var bookings = new FakeBookings { Down = true };

        var dto = await new GetOperationsOverviewUseCase(repo, bookings, new StalenessOptions()).ExecuteAsync(3);

        Assert.Equal(0, dto.ActiveBookings);
        Assert.Equal(0, dto.UpcomingBookings);
        Assert.Null(dto.LastHeartbeatAtUtc);
    }

    [Fact]
    public async Task Overview_shows_emergency_state_when_suspended()
    {
        var lot = Lot();
        lot.ApplyEmergencyStop("Cháy hệ thống điện", Now);
        var dto = await new GetOperationsOverviewUseCase(new FakeRepo([lot]), new FakeBookings(), new StalenessOptions())
            .ExecuteAsync(3);

        Assert.Equal("Suspended", dto.Status);
        Assert.True(dto.EmergencyStopped);
        Assert.Equal("Cháy hệ thống điện", dto.EmergencyStopReason);
        Assert.Equal(Now, dto.EmergencyStoppedAtUtc);
    }

    [Fact]
    public async Task Overview_throws_for_unknown_lot()
        => await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetOperationsOverviewUseCase(new FakeRepo([]), new FakeBookings(), new StalenessOptions()).ExecuteAsync(99));


    // ===== US-069: ParkingLot emergency stop =====

    [Fact]
    public void Emergency_stop_suspends_lot_with_reason_and_is_idempotent()
    {
        var lot = Lot();
        lot.ApplyEmergencyStop("Cháy hệ thống điện", Now);
        lot.ApplyEmergencyStop("Lý do khác", Now.AddMinutes(1)); // re-stop → giữ nguyên trạng thái đầu

        Assert.Equal(ParkingLotStatus.Suspended, lot.Status);
        Assert.Equal("Cháy hệ thống điện", lot.EmergencyStopReason);
        Assert.Equal(Now, lot.EmergencyStoppedAtUtc);
    }

    [Fact]
    public void Lift_emergency_stop_returns_lot_to_active_and_clears_reason()
    {
        var lot = Lot();
        lot.ApplyEmergencyStop("Cháy hệ thống điện", Now);

        lot.LiftEmergencyStop(Now.AddMinutes(30));

        Assert.Equal(ParkingLotStatus.Active, lot.Status);
        Assert.Null(lot.EmergencyStopReason);
        Assert.Null(lot.EmergencyStoppedAtUtc);
    }

    [Fact]
    public void Lift_emergency_stop_is_noop_when_not_suspended()
    {
        var lot = Lot();
        lot.LiftEmergencyStop(Now);
        Assert.Equal(ParkingLotStatus.Active, lot.Status);
    }

    // ===== US-069: EmergencyStopUseCase =====

    [Fact]
    public async Task Emergency_stop_requires_explicit_confirmation()
    {
        var useCase = new EmergencyStopUseCase(new FakeRepo([Lot()]), new FakeBookings(), new RecordingEvents(), new FixedClock(Now));
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(3, 9, "LotOwner", "Cháy", confirm: false));
        Assert.Contains("confirm", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ok")] // < 5 ký tự
    public async Task Emergency_stop_requires_meaningful_reason(string reason)
    {
        var useCase = new EmergencyStopUseCase(new FakeRepo([Lot()]), new FakeBookings(), new RecordingEvents(), new FixedClock(Now));
        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(3, 9, "LotOwner", reason, confirm: true));
    }

    [Fact]
    public async Task Emergency_stop_writes_audit_log_publishes_event_and_blocks_lot()
    {
        var lot = Lot();
        var repo = new FakeRepo([lot]);
        var events = new RecordingEvents();
        var affected = new List<EmergencyAffectedBooking> { Affected(12, "BK-0001", 5), Affected(13, "BK-0002", 6) };
        var useCase = new EmergencyStopUseCase(repo, new FakeBookings { Affected = affected }, events, new FixedClock(Now));

        var result = await useCase.ExecuteAsync(3, actorUserId: 9, actorRole: "LotOwner", "Cháy hệ thống điện tầng B1", confirm: true);

        Assert.Equal("Suspended", result.Status);
        Assert.Equal(2, result.AffectedBookings);

        var log = Assert.Single(repo.Logs);
        Assert.Equal("Stop", log.Action);
        Assert.Equal(9, log.ActorUserId);
        Assert.Equal("LotOwner", log.ActorRole);
        Assert.Equal("Cháy hệ thống điện tầng B1", log.Reason);
        Assert.Equal(2, log.AffectedBookings);
        Assert.Equal(3, log.ParkingLotId);

        var evt = Assert.IsType<LotEmergencyStopped>(Assert.Single(events.Events));
        Assert.Equal(3, evt.ParkingLotId);
        Assert.Equal(9, evt.StoppedByUserId);
        Assert.Equal(2, evt.AffectedBookings.Count);
        Assert.Equal("BK-0001", evt.AffectedBookings[0].Code);
        Assert.Equal(ParkingLotStatus.Suspended, lot.Status);
        Assert.Equal(1, repo.SaveCalls);
    }

    [Fact]
    public async Task Emergency_stop_is_idempotent_on_already_suspended_lot()
    {
        var lot = Lot();
        lot.ApplyEmergencyStop("Cháy", Now);
        var repo = new FakeRepo([lot]);
        var useCase = new EmergencyStopUseCase(repo, new FakeBookings(), new RecordingEvents(), new FixedClock(Now));

        var result = await useCase.ExecuteAsync(3, 9, "LotOwner", "Cháy lại lần 2", confirm: true);

        Assert.Equal("already-stopped", result.AuditLogId);
        Assert.Equal(0, result.AffectedBookings);
        Assert.Empty(repo.Logs);
    }

    [Fact]
    public async Task Emergency_stop_throws_for_unknown_lot()
    {
        var useCase = new EmergencyStopUseCase(new FakeRepo([]), new FakeBookings(), new RecordingEvents(), new FixedClock(Now));
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(99, 9, "Staff", "Cháy bất thường", confirm: true));
    }


    // ===== US-069: LiftEmergencyStopUseCase =====

    [Fact]
    public async Task Lift_requires_reason_and_publishes_event_with_audit()
    {
        var lot = Lot();
        lot.ApplyEmergencyStop("Cháy", Now);
        var repo = new FakeRepo([lot]);
        var events = new RecordingEvents();
        var useCase = new LiftEmergencyStopUseCase(repo, events, new FixedClock(Now.AddMinutes(30)));

        var result = await useCase.ExecuteAsync(3, 9, "Staff", "Đã xử lý xong sự cố", default);

        Assert.Equal("Active", result.Status);
        var log = Assert.Single(repo.Logs);
        Assert.Equal("Lift", log.Action);
        Assert.Equal("Đã xử lý xong sự cố", log.Reason);
        var evt = Assert.IsType<LotEmergencyLifted>(Assert.Single(events.Events));
        Assert.Equal(9, evt.LiftedByUserId);
    }

    [Fact]
    public async Task Lift_without_reason_is_rejected()
    {
        var lot = Lot();
        lot.ApplyEmergencyStop("Cháy", Now);
        var useCase = new LiftEmergencyStopUseCase(new FakeRepo([lot]), new RecordingEvents(), new FixedClock(Now));
        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(3, 9, "Staff", "  ", default));
    }

    [Fact]
    public async Task Lift_is_noop_when_lot_not_suspended()
    {
        var repo = new FakeRepo([Lot()]);
        var events = new RecordingEvents();
        var useCase = new LiftEmergencyStopUseCase(repo, events, new FixedClock(Now));

        var result = await useCase.ExecuteAsync(3, 9, "Staff", "Mở lại", default);

        Assert.Equal("not-suspended", result.AuditLogId);
        Assert.Empty(repo.Logs);
        Assert.Empty(events.Events);
    }
}

