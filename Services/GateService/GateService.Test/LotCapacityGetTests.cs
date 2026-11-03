using GateService.Application.Features.LotCapacity;
using GateService.Domain.Entities;
using GateService.Domain.Enums;

namespace GateService.Test;

/// <summary>US-066 (luật 5) – GET tính availableFor* và báo "configured = false" khi chưa có cấu hình.</summary>
public class LotCapacityGetTests
{
    [Fact]
    public async Task Not_configured_lot_returns_zeros_with_flag()
    {
        var useCase = new GetLotCapacityConfigUseCase(new FakeStore(), new FakeCounter());

        var dto = await useCase.ExecuteAsync(9);

        Assert.False(dto.Configured);
        Assert.Equal(0, dto.Capacity);
        Assert.Equal(0, dto.AvailableForSubscriber);
    }

    [Fact]
    public async Task Availability_is_computed_per_rule_five()
    {
        // C=50, B=10, T=45, V=5 → availableForSubscriber = 50-45 = 5;
        // availableForTransient = max(0, min(C-T, B-V)) = max(0, min(5, 5)) = 5.
        var store = new FakeStore
        {
            Policy = new LotCapacityConfig { ParkingLotId = 3, Capacity = 50, TransientBuffer = 10, SubscriberQuota = 10, Version = 4 },
            Counters = new AdmissionCounters(Total: 45, Transient: 5, Subscriber: 40),
        };

        var dto = await new GetLotCapacityConfigUseCase(store, new FakeCounter()).ExecuteAsync(3);

        Assert.True(dto.Configured);
        Assert.Equal(5, dto.AvailableForSubscriber);
        Assert.Equal(5, dto.AvailableForTransient);
        Assert.Equal(45, dto.TotalParked);
        Assert.Equal(8, dto.ActiveSubscribers);
    }

    [Fact]
    public async Task Transient_availability_is_zero_when_buffer_exhausted_even_if_lot_has_room()
    {
        // C=50, B=2, T=10, V=2 → vãng lai hết buffer: availableForTransient = 0 (dù còn 40 chỗ tổng).
        var store = new FakeStore
        {
            Policy = new LotCapacityConfig { ParkingLotId = 4, Capacity = 50, TransientBuffer = 2, SubscriberQuota = 10, Version = 1 },
            Counters = new AdmissionCounters(Total: 10, Transient: 2, Subscriber: 8),
        };

        var dto = await new GetLotCapacityConfigUseCase(store, new FakeCounter()).ExecuteAsync(4);

        Assert.Equal(0, dto.AvailableForTransient);
        Assert.Equal(40, dto.AvailableForSubscriber);
    }

    private sealed class FakeStore : ILotCapacityPolicyStore
    {
        public LotCapacityConfig? Policy { get; set; }
        public AdmissionCounters? Counters { get; set; }
        public Task<LotCapacityConfig?> GetAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult(Policy is not null && Policy.ParkingLotId == parkingLotId ? Policy : null);
        public Task<AdmissionCounters?> GetCountersAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult(Counters);
        public Task AddAsync(LotCapacityConfig config, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddAuditAsync(LotCapacityAuditLog log, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeCounter : IActiveSubscriberCounter
    {
        public Task<int> CountActiveAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult(8);
    }
}