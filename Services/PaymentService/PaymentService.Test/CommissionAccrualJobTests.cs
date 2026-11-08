using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using PaymentService.Application.Features.Commission;
using PaymentService.Infrastructure;
using PaymentService.Infrastructure.Commission;

namespace PaymentService.Test;

// US-079: BackgroundService chạy use case trong scope riêng; lỗi một lượt không làm dừng job; tắt được bằng cấu hình.
public class CommissionAccrualJobTests
{
    private static readonly AdminServiceConnection Configured = new(new Uri("http://localhost:5108/"), "k", TimeSpan.FromSeconds(5));

    private sealed class CountingUseCase(Func<CommissionRunResult> run) : IRunCommissionAccrualUseCase
    {
        public int Calls { get; private set; }

        public Task<CommissionRunResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(run());
        }
    }

    private static (CommissionAccrualJob Job, CountingUseCase UseCase) Job(Func<CommissionRunResult> run, bool enabled = true)
    {
        var useCase = new CountingUseCase(run);
        var provider = new ServiceCollection().AddScoped<IRunCommissionAccrualUseCase>(_ => useCase).BuildServiceProvider();
        var job = new CommissionAccrualJob(provider.GetRequiredService<IServiceScopeFactory>(),
            new CommissionJobSchedule(enabled, TimeSpan.FromHours(1)), Configured, TimeProvider.System, NullLogger<CommissionAccrualJob>.Instance);
        return (job, useCase);
    }

    [Fact]
    public async Task RunOnce_ReturnsUseCaseResult()
    {
        var (job, _) = Job(() => new CommissionRunResult(false, 3, 3, 1, 1, 0, null));
        var result = await job.RunOnceAsync(CancellationToken.None);
        Assert.Equal(4, result!.Processed);
    }

    [Fact]
    public async Task RunOnce_SwallowsErrors_SoNextTickRetries()
    {
        var (job, useCase) = Job(() => throw new InvalidOperationException("database down"));

        Assert.Null(await job.RunOnceAsync(CancellationToken.None));
        Assert.Null(await job.RunOnceAsync(CancellationToken.None));
        Assert.Equal(2, useCase.Calls);
    }

    [Fact]
    public async Task Start_RunsImmediately_ThenWaitsForInterval()
    {
        var (job, useCase) = Job(() => CommissionRunResult.SkippedRun);

        await job.StartAsync(CancellationToken.None);
        for (var i = 0; i < 50 && useCase.Calls == 0; i++) await Task.Delay(20);
        await job.StopAsync(CancellationToken.None);

        Assert.Equal(1, useCase.Calls);
    }

    [Fact]
    public async Task Disabled_NeverRuns()
    {
        var (job, useCase) = Job(() => CommissionRunResult.SkippedRun, enabled: false);

        await job.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await job.StopAsync(CancellationToken.None);

        Assert.Equal(0, useCase.Calls);
    }

    [Fact]
    public void AddPaymentCommission_RegistersJobAndUseCases()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Commission:ExcludedPurposes:0"] = "Overstay",
            ["Commission:IntervalSeconds"] = "1",
        }).Build();
        var services = new ServiceCollection().AddPaymentCommission(configuration);

        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(CommissionAccrualJob));
        Assert.Contains(services, d => d.ServiceType == typeof(IRunCommissionAccrualUseCase));
        Assert.Contains(services, d => d.ServiceType == typeof(IGetAdminCommissionSummaryUseCase));
        var schedule = (CommissionJobSchedule)services.Single(d => d.ServiceType == typeof(CommissionJobSchedule)).ImplementationInstance!;
        Assert.Equal(TimeSpan.FromSeconds(CommissionJobSchedule.MinIntervalSeconds), schedule.Interval);   // tối thiểu 5 giây
        var policy = (CommissionPolicy)services.Single(d => d.ServiceType == typeof(CommissionPolicy)).ImplementationInstance!;
        Assert.Equal([ParkingManagement.SharedKernel.Enums.PaymentPurpose.Overstay], policy.ExcludedPurposes);
        Assert.False(((AdminServiceConnection)services.Single(d => d.ServiceType == typeof(AdminServiceConnection)).ImplementationInstance!)
            .IsConfigured);                                                                                  // chưa có khóa → job vẫn chạy Reversal
    }
}
