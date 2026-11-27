using AdminService.Application.Features;
using AdminService.Application.Features.Settings;
using AdminService.Domain.Entities;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-099: bật/tắt feature flag (unit test với fake repository, không cần database).
public class FeatureFlagTests
{
    private const string Key = "FEATURE_3D_MAP";
    private const int Admin = 1;
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 8, 0, 0, TimeSpan.Zero);

    private static UpdateFeatureFlagUseCase UseCase(FakeRepository repository) => new(repository, new FixedTimeProvider(Now));

    [Fact]
    public async Task Enabling_a_flag_returns_new_state_and_writes_audit_log()
    {
        var repository = new FakeRepository(new FeatureFlagDto(Key, false, "Sơ đồ 3D"));

        var result = await UseCase(repository).ExecuteAsync(Key, new(true, "  Mở thử 3D cho demo  "), Admin);

        Assert.Equal(new FeatureFlagDto(Key, true, "Sơ đồ 3D", Admin, Now.UtcDateTime), result);
        var audit = Assert.Single(repository.AuditLogs);
        Assert.Equal(("AdminService", Admin, "FeatureFlag.Updated", "FeatureFlag", Key),
            (audit.SourceService, audit.UserId!.Value, audit.Action, audit.EntityName, audit.EntityId));
        Assert.Equal("""{"isEnabled":false}""", audit.OldValuesJson);
        Assert.Equal("""{"isEnabled":true}""", audit.NewValuesJson);
        Assert.Equal("Mở thử 3D cho demo", audit.Reason);
    }

    [Fact]
    public async Task Disabling_a_flag_records_the_reverse_values()
    {
        var repository = new FakeRepository(new FeatureFlagDto(Key, true, null));

        var result = await UseCase(repository).ExecuteAsync(Key, new(false, "Tắt vì lỗi"), Admin);

        Assert.False(result.IsEnabled);
        var audit = Assert.Single(repository.AuditLogs);
        Assert.Equal("""{"isEnabled":true}""", audit.OldValuesJson);
        Assert.Equal("""{"isEnabled":false}""", audit.NewValuesJson);
    }

    [Fact]
    public async Task Key_is_trimmed_and_not_case_sensitive()
    {
        var repository = new FakeRepository(new FeatureFlagDto(Key, false, null));

        var result = await UseCase(repository).ExecuteAsync(" feature_3d_map ", new(true, "Lý do"), Admin);

        Assert.Equal(Key, result.Key);
    }

    [Fact]
    public async Task Setting_the_current_value_conflicts_and_writes_nothing()
    {
        var repository = new FakeRepository(new FeatureFlagDto(Key, true, null));

        await Assert.ThrowsAsync<ConflictException>(() => UseCase(repository).ExecuteAsync(Key, new(true, "Lý do"), Admin));

        Assert.Empty(repository.AuditLogs);
    }

    [Fact]
    public async Task Losing_a_concurrent_change_conflicts()
    {
        var repository = new FakeRepository(new FeatureFlagDto(Key, false, null)) { LoseRace = true };

        var ex = await Assert.ThrowsAsync<ConflictException>(() => UseCase(repository).ExecuteAsync(Key, new(true, "Lý do"), Admin));

        Assert.Contains("vừa được đổi", ex.Message);
        Assert.Empty(repository.AuditLogs);
    }

    [Fact]
    public async Task Unknown_flag_is_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => UseCase(new FakeRepository()).ExecuteAsync("FEATURE_X", new(true, "Lý do"), Admin));

    [Theory]
    [InlineData(null, "Lý do", Admin)]        // thiếu isEnabled
    [InlineData(true, null, Admin)]           // thiếu lý do
    [InlineData(true, "   ", Admin)]          // lý do toàn khoảng trắng
    [InlineData(true, "Lý do", 0)]            // không rõ người thao tác
    public async Task Invalid_requests_are_rejected(bool? isEnabled, string? reason, int performedBy)
    {
        var repository = new FakeRepository(new FeatureFlagDto(Key, false, null));

        await Assert.ThrowsAsync<ValidationException>(() => UseCase(repository).ExecuteAsync(Key, new(isEnabled, reason), performedBy));

        Assert.Empty(repository.AuditLogs);
    }

    [Fact]
    public async Task Reason_longer_than_1000_characters_is_rejected()
        => await Assert.ThrowsAsync<ValidationException>(() => UseCase(new FakeRepository(new FeatureFlagDto(Key, false, null)))
            .ExecuteAsync(Key, new(true, new string('x', 1001)), Admin));

    [Fact]
    public async Task Flag_states_expose_only_key_and_enabled()
    {
        var states = await new GetFeatureFlagStatesUseCase(new FakeQueries()).ExecuteAsync();

        Assert.Equal([new FeatureFlagStateDto("FEATURE_3D_MAP", false), new FeatureFlagStateDto("FEATURE_VNPAY", true)], states);
    }

    private sealed class FakeRepository(params FeatureFlagDto[] flags) : IFeatureFlagRepository
    {
        public bool LoseRace { get; init; }
        public List<AuditLog> AuditLogs { get; } = [];

        public Task<FeatureFlagDto?> FindByKeyAsync(string key, CancellationToken cancellationToken)
            => Task.FromResult(flags.FirstOrDefault(f => f.Key == key));

        public Task<bool> TryChangeAsync(string key, bool isEnabled, int performedByUserId, DateTime nowUtc, AuditLog auditLog, CancellationToken cancellationToken)
        {
            if (LoseRace) return Task.FromResult(false);
            AuditLogs.Add(auditLog);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeQueries : ISettingsQueries
    {
        public Task<IReadOnlyList<SystemConfigRecord>> ListConfigsAsync(DateTime changesUpToUtc, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SystemConfigRecord>>([]);

        public Task<SystemConfigRecord?> FindConfigByKeyAsync(string key, DateTime changesUpToUtc, CancellationToken cancellationToken)
            => Task.FromResult<SystemConfigRecord?>(null);

        public Task<IReadOnlyList<FeatureFlagDto>> ListFeatureFlagsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<FeatureFlagDto>>(
                [new("FEATURE_3D_MAP", false, "Mô tả nội bộ", 7, Now.UtcDateTime), new("FEATURE_VNPAY", true, null)]);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
