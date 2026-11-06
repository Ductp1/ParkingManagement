using AdminService.Application.Features;
using AdminService.Application.Features.Commission;
using AdminService.Application.Features.Dashboard;
using AdminService.Application.Features.Settings;
using Microsoft.Extensions.Logging;

namespace AdminService.Test;

// US-079: test double dùng chung cho use case và endpoint tỷ lệ hoa hồng.

internal sealed class FakeCommissionRateQueries(params SignedLotContractRate[] contracts) : ICommissionRateQueries
{
    public List<IReadOnlyCollection<int>?> Requests { get; } = [];

    public Task<IReadOnlyList<SignedLotContractRate>> ListSignedContractsAsync(IReadOnlyCollection<int>? parkingLotIds,
        CancellationToken cancellationToken)
    {
        Requests.Add(parkingLotIds);
        return Task.FromResult<IReadOnlyList<SignedLotContractRate>>(
            [.. contracts.Where(c => parkingLotIds is null || parkingLotIds.Contains(c.ParkingLotId))]);
    }
}

/// <summary>DEFAULT_COMMISSION_RATE (giá trị gốc + lịch sử thay đổi); lọc changesUpToUtc như SettingsQueries thật. null = chưa có tham số.</summary>
internal sealed class FakeCommissionSettings(string? defaultValue = "0.10", params SystemConfigChangePoint[] changes) : ISettingsQueries
{
    public Task<IReadOnlyList<SystemConfigRecord>> ListConfigsAsync(DateTime changesUpToUtc, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<SystemConfigRecord?> FindConfigByKeyAsync(string key, DateTime changesUpToUtc, CancellationToken cancellationToken)
        => Task.FromResult(defaultValue is null || key != CommissionRateResolver.DefaultRateKey
            ? null
            : new SystemConfigRecord(1, key, defaultValue, "decimal", null, [.. changes.Where(c => c.EffectiveFromUtc <= changesUpToUtc)]));

    public Task<IReadOnlyList<FeatureFlagDto>> ListFeatureFlagsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
}

internal sealed class FakeParkingLots(Func<CancellationToken, Task<SourceResult<ParkingLotsSource>>> handler) : IParkingDashboardClient
{
    public static FakeParkingLots Returning(params ParkingLotSource[] lots)
        => new(_ => Task.FromResult(SourceResult<ParkingLotsSource>.Ok(new ParkingLotsSource(DateTime.UtcNow, lots))));

    public static FakeParkingLots Failing(string detail = "ParkingService trả 500.")
        => new(_ => Task.FromResult(SourceResult<ParkingLotsSource>.Error(detail)));

    public Task<SourceResult<ParkingLotsSource>> GetLotsSummaryAsync(CancellationToken cancellationToken) => handler(cancellationToken);
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add((logLevel, formatter(state, exception)));
}
