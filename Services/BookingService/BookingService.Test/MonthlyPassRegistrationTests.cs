using BookingService.Application.Features.MonthlyPasses;
using BookingService.Domain.Entities;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace BookingService.Test;

/// <summary>
/// US-066 (luật 4) – đăng ký thuê bao: được khi A &lt; Q; từ chối khi A = Q;
/// race thua ở store → vẫn chặn với SUBSCRIBER_QUOTA_FULL; chưa cấu hình Q → không giới hạn.
/// </summary>
public class MonthlyPassRegistrationTests
{
    [Fact]
    public async Task Registers_active_pass_when_quota_available()
    {
        var store = new FakeStore(activeCount: 3, addResult: true);
        var useCase = new RegisterMonthlyPassUseCase(store, new FixedQuota(5), TimeProvider.System);

        var dto = await useCase.ExecuteAsync(new RegisterMonthlyPassCommand(3, 7, 11, "51F-123.45", Months: 2));

        Assert.Equal(MonthlyPassStatus.Active.ToString(), dto.Status);
        Assert.Equal("51F-123.45", dto.PlateNumber);
        Assert.True(store.TryAddCalled);
        Assert.Equal(5, store.CapturedQuota);
        Assert.Equal(dto.ValidFrom.AddMonths(2), dto.ValidTo);
    }

    [Fact]
    public async Task Rejected_when_active_subscriptions_reach_quota()
    {
        var store = new FakeStore(activeCount: 3, addResult: true);
        var useCase = new RegisterMonthlyPassUseCase(store, new FixedQuota(3), TimeProvider.System);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(new RegisterMonthlyPassCommand(3, 7, 11, "51F-123.45")));

        Assert.Contains("SUBSCRIBER_QUOTA_FULL", ex.Message);
        Assert.False(store.TryAddCalled); // không chèn khi đã biết hết hạn mức
    }

    [Fact]
    public async Task Losing_concurrent_race_is_reported_as_quota_full()
    {
        var store = new FakeStore(activeCount: 2, addResult: false); // advisory lock check thấy đầy
        var useCase = new RegisterMonthlyPassUseCase(store, new FixedQuota(3), TimeProvider.System);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(new RegisterMonthlyPassCommand(3, 7, 11, "51F-123.45")));

        Assert.Contains("SUBSCRIBER_QUOTA_FULL", ex.Message);
        Assert.Contains("đồng thời", ex.Message);
    }

    [Fact]
    public async Task Lot_without_capacity_config_is_unrestricted()
    {
        var store = new FakeStore(activeCount: 99, addResult: true);
        var useCase = new RegisterMonthlyPassUseCase(store, new FixedQuota(null), TimeProvider.System);

        var dto = await useCase.ExecuteAsync(new RegisterMonthlyPassCommand(3, 7, 11, "51F-123.45"));

        Assert.Equal(MonthlyPassStatus.Active.ToString(), dto.Status);
        Assert.True(store.TryAddCalled);
        Assert.Equal(int.MaxValue, store.CapturedQuota); // chưa cấu hình Q → không giới hạn (đưa qua bằng ngân sách vô hạn)
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public async Task Invalid_months_is_rejected(int months)
    {
        var store = new FakeStore(activeCount: 0, addResult: true);
        var useCase = new RegisterMonthlyPassUseCase(store, new FixedQuota(5), TimeProvider.System);

        await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(new RegisterMonthlyPassCommand(3, 7, 11, "51F-123.45", Months: months)));
    }

    private sealed class FakeStore(int activeCount, bool addResult) : IMonthlyPassRegistrationStore
    {
        public bool TryAddCalled { get; private set; }
        public int? CapturedQuota { get; private set; }

        public Task<int> CountActiveAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult(activeCount);

        public Task<bool> TryAddActiveWithinQuotaAsync(MonthlyPass pass, int subscriberQuota, CancellationToken cancellationToken = default)
        {
            TryAddCalled = true;
            CapturedQuota = subscriberQuota;
            return Task.FromResult(addResult);
        }
    }

    private sealed class FixedQuota(int? quota) : ILotSubscriberQuotaProvider
    {
        public Task<int?> GetSubscriberQuotaAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult(quota);
    }
}