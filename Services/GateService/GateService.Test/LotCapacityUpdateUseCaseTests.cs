using GateService.Application.Features.LotCapacity;
using GateService.Domain.Entities;
using GateService.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace GateService.Test;

/// <summary>US-066 – use case lưu cấu hình: tạo lần đầu; 409 version; luật 7a/7b/7c; publish lỗi không hỏng lưu.</summary>
public class LotCapacityUpdateUseCaseTests
{
    private readonly FakeStore _store = new();
    private readonly FakePublisher _publisher = new();

    private UpdateLotCapacityConfigUseCase CreateUseCase()
        => new(_store, new FakeSubscriberCounter(), _publisher, TimeProvider.System,
            NullLogger<UpdateLotCapacityConfigUseCase>.Instance);

    private static UpdateLotCapacityCommand Command(int c = 50, int b = 10, int q = 10, long version = 0)
        => new(3, c, b, q, version, ChangedByUserId: 1);

    [Fact]
    public async Task First_save_creates_config_with_version_1_audit_and_publishes()
    {
        var result = await CreateUseCase().ExecuteAsync(Command());

        Assert.True(result.Saved);
        Assert.Equal(1, result.Version);
        Assert.Empty(result.Warnings);
        Assert.Equal(1, _store.SaveCount);
        var audit = Assert.Single(_store.Audits);
        Assert.Equal(50, audit.NewCapacity);
        Assert.Equal(1, audit.NewVersion);
        Assert.Contains(3, _publisher.Calls);
    }

    [Fact]
    public async Task Stale_version_conflicts()
    {
        _store.Policy = new LotCapacityConfig { ParkingLotId = 3, Capacity = 50, TransientBuffer = 10, SubscriberQuota = 10, Version = 3 };

        await Assert.ThrowsAsync<StaleCapacityConfigVersionException>(() => CreateUseCase().ExecuteAsync(Command(version: 2)));
    }

    [Fact]
    public async Task Reducing_capacity_below_current_occupancy_is_rejected_with_occupancy_in_message()
    {
        _store.Policy = new LotCapacityConfig { ParkingLotId = 3, Capacity = 50, TransientBuffer = 10, SubscriberQuota = 10, Version = 2 };
        _store.Counters = new AdmissionCounters(Total: 10, Transient: 4, Subscriber: 6);

        // b = 0, q = 0 để đi qua luật 1 và chạm đúng luật 7a (C mới < T).
        var ex = await Assert.ThrowsAsync<CapacityValidationException>(
            () => CreateUseCase().ExecuteAsync(Command(c: 9, b: 0, q: 0, version: 2)));

        var error = Assert.Single(ex.Errors["capacity"]);
        Assert.Equal(LotCapacityValidator.CodeCapacityBelowOccupancy, error.Code);
        Assert.Contains("10 xe", error.Message);
    }

    [Fact]
    public async Task Lowering_buffer_and_quota_below_runtime_saves_with_two_warnings()
    {
        _store.Policy = new LotCapacityConfig { ParkingLotId = 3, Capacity = 50, TransientBuffer = 10, SubscriberQuota = 10, Version = 2 };
        _store.Counters = new AdmissionCounters(Total: 8, Transient: 5, Subscriber: 3);

        var result = await CreateUseCase().ExecuteAsync(Command(c: 50, b: 3, q: 4, version: 2));

        Assert.True(result.Saved);
        Assert.Equal(2, result.Warnings.Count);
        Assert.Contains("V = 5", result.Warnings[0]);
        Assert.Contains("A = 7", result.Warnings[1]);
        Assert.Equal(3, _store.Policy!.TransientBuffer);
        Assert.Equal(3, result.Version);
        Assert.Single(_store.Audits);
    }

    [Fact]
    public async Task Publisher_failure_does_not_break_saving()
    {
        _publisher.ThrowOnPublish = true;

        var result = await CreateUseCase().ExecuteAsync(Command());

        Assert.True(result.Saved); // nghiệp vụ chính không hỏng vì publish
    }

    // ===== Fakes =====
    private sealed class FakeStore : ILotCapacityPolicyStore
    {
        public LotCapacityConfig? Policy { get; set; }
        public AdmissionCounters? Counters { get; set; }
        public List<LotCapacityAuditLog> Audits { get; } = [];
        public int SaveCount { get; private set; }

        public Task<LotCapacityConfig?> GetAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult(Policy is not null && Policy.ParkingLotId == parkingLotId ? Policy : null);

        public Task<AdmissionCounters?> GetCountersAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult(Counters);

        public Task AddAsync(LotCapacityConfig config, CancellationToken cancellationToken = default)
        {
            Policy = config;
            return Task.CompletedTask;
        }

        public Task AddAuditAsync(LotCapacityAuditLog log, CancellationToken cancellationToken = default)
        {
            Audits.Add(log);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePublisher : ILotCapacityRealtimePublisher
    {
        public List<int> Calls { get; } = [];
        public bool ThrowOnPublish { get; set; }

        public Task PublishCapacityChangedAsync(int parkingLotId, CancellationToken cancellationToken = default)
        {
            if (ThrowOnPublish) throw new InvalidOperationException("SignalR down");
            Calls.Add(parkingLotId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSubscriberCounter : IActiveSubscriberCounter
    {
        public Task<int> CountActiveAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult(7);
    }
}