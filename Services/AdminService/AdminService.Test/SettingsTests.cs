using AdminService.Application.Features;
using AdminService.Application.Features.Settings;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// UC-44 / US-098: đọc tham số hệ thống theo thời điểm (unit test với fake ISettingsQueries, không cần database).
public class SettingsTests
{
    private const string HoldKey = "BOOKING_HOLD_MINUTES";
    private const string CancellationKey = "CANCELLATION_WINDOW_MINUTES";

    private static readonly DateTimeOffset Now = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTime NowUtc = Now.UtcDateTime;

    // ===== GET /api/v1/admin/settings =====

    [Fact]
    public async Task Settings_combine_configs_and_feature_flags()
    {
        var queries = new FakeQueries(Config(1, CancellationKey, "60"));

        var result = await ListUseCase(queries).ExecuteAsync();

        // Tương thích ngược: tham số chưa từng đổi vẫn trả giá trị gốc, UpdatedAtUtc = null; feature flag giữ nguyên.
        var config = Assert.Single(result.Configs);
        Assert.Equal(new SystemConfigDto(CancellationKey, "60", "int", "Mô tả", null), config);
        Assert.Contains(result.FeatureFlags, f => f.Key == "FEATURE_3D_MAP" && !f.IsEnabled);
    }

    [Fact]
    public async Task Settings_without_at_return_values_in_effect_now()
    {
        var queries = new FakeQueries(
            Config(1, HoldKey, "15", Change(1, "20", NowUtc.AddDays(-2)), Change(2, "30", NowUtc.AddDays(3))),
            Config(2, CancellationKey, "60"));

        var result = await ListUseCase(queries).ExecuteAsync();

        var hold = result.Configs.Single(c => c.Key == HoldKey);
        Assert.Equal("20", hold.Value);                                  // thay đổi lên lịch 3 ngày nữa chưa áp dụng
        Assert.Equal(NowUtc.AddDays(-2), hold.UpdatedAtUtc);
        Assert.Equal("60", result.Configs.Single(c => c.Key == CancellationKey).Value);
        Assert.Equal(NowUtc, queries.ChangesUpToUtc);
    }

    [Fact]
    public async Task Settings_at_a_past_moment_return_values_in_effect_then()
    {
        // AC2: booking tạo 5 ngày trước chốt lại tham số của lúc đó, dù Admin đã đổi sau.
        var queries = new FakeQueries(Config(1, HoldKey, "15", Change(1, "20", NowUtc.AddDays(-2))));
        var bookingCreatedAt = NowUtc.AddDays(-5);

        var result = await ListUseCase(queries).ExecuteAsync(bookingCreatedAt);

        var hold = Assert.Single(result.Configs);
        Assert.Equal("15", hold.Value);
        Assert.Null(hold.UpdatedAtUtc);
        Assert.Equal(bookingCreatedAt, queries.ChangesUpToUtc);
    }

    [Fact]
    public async Task Settings_at_a_future_moment_include_scheduled_changes()
    {
        var queries = new FakeQueries(Config(1, HoldKey, "15", Change(1, "30", NowUtc.AddDays(3))));

        var result = await ListUseCase(queries).ExecuteAsync(NowUtc.AddDays(3));

        Assert.Equal("30", Assert.Single(result.Configs).Value);
    }

    [Fact]
    public async Task Settings_at_without_time_zone_is_read_as_utc()
    {
        var queries = new FakeQueries(Config(1, HoldKey, "15"));
        var unspecified = DateTime.SpecifyKind(NowUtc.AddDays(-1), DateTimeKind.Unspecified);

        await ListUseCase(queries).ExecuteAsync(unspecified);

        Assert.Equal(DateTimeKind.Utc, queries.ChangesUpToUtc!.Value.Kind);   // timestamptz chỉ nhận UTC
        Assert.Equal(unspecified.Ticks, queries.ChangesUpToUtc.Value.Ticks);
    }

    [Fact]
    public async Task Settings_skip_cancelled_changes()
    {
        var queries = new FakeQueries(
            Config(1, HoldKey, "15", Change(1, "20", NowUtc.AddDays(-1), cancelledAtUtc: NowUtc.AddDays(-2))));

        var result = await ListUseCase(queries).ExecuteAsync();

        Assert.Equal("15", Assert.Single(result.Configs).Value);
    }

    [Fact]
    public async Task Feature_flags_do_not_depend_on_at()
    {
        var queries = new FakeQueries(Config(1, HoldKey, "15"));

        var past = await ListUseCase(queries).ExecuteAsync(NowUtc.AddYears(-1));
        var current = await ListUseCase(queries).ExecuteAsync();

        Assert.Equal(current.FeatureFlags, past.FeatureFlags);
    }

    // ===== GET /api/v1/admin/settings/{key} =====

    [Fact]
    public async Task Config_by_key_returns_effective_value_with_default_value()
    {
        var queries = new FakeQueries(
            Config(1, HoldKey, "15", Change(1, "20", NowUtc.AddDays(-2)), Change(2, "30", NowUtc.AddDays(3))));

        var dto = await ByKeyUseCase(queries).ExecuteAsync(HoldKey);

        Assert.Equal(HoldKey, dto.Key);
        Assert.Equal("20", dto.Value);
        Assert.Equal("int", dto.DataType);
        Assert.Equal("Mô tả", dto.Description);
        Assert.Equal("15", dto.DefaultValue);
        Assert.Equal(NowUtc.AddDays(-2), dto.EffectiveFromUtc);
        Assert.Equal(NowUtc, queries.ChangesUpToUtc);
    }

    [Fact]
    public async Task Config_by_key_exposes_the_rule_used_when_changing_its_value()
    {
        var queries = new FakeQueries(
            Config(1, HoldKey, "15"),
            new SystemConfigRecord(2, "SETTLEMENT_CYCLE", "WEEKLY", "string", null, []),
            new SystemConfigRecord(3, "KEY_WITHOUT_RULE", "abc", "string", null, []));

        var hold = await ByKeyUseCase(queries).ExecuteAsync(HoldKey);
        var cycle = await ByKeyUseCase(queries).ExecuteAsync("SETTLEMENT_CYCLE");
        var free = await ByKeyUseCase(queries).ExecuteAsync("KEY_WITHOUT_RULE");

        Assert.Equal(("1", "120"), (hold.MinValue, hold.MaxValue));
        Assert.Null(hold.AllowedValues);
        Assert.Equal(["WEEKLY", "BIWEEKLY", "MONTHLY"], cycle.AllowedValues);
        Assert.Null(cycle.MinValue);
        Assert.Null(cycle.MaxValue);
        Assert.Null(free.MinValue);
        Assert.Null(free.MaxValue);
        Assert.Null(free.AllowedValues);
    }

    [Fact]
    public async Task Config_by_key_never_changed_returns_default_value_without_effective_date()
    {
        var dto = await ByKeyUseCase(new FakeQueries(Config(1, HoldKey, "15"))).ExecuteAsync(HoldKey);

        Assert.Equal("15", dto.Value);
        Assert.Equal("15", dto.DefaultValue);
        Assert.Null(dto.EffectiveFromUtc);
    }

    [Fact]
    public async Task Config_by_key_at_a_past_moment_returns_value_in_effect_then()
    {
        var queries = new FakeQueries(
            Config(1, HoldKey, "15", Change(1, "20", NowUtc.AddDays(-5)), Change(2, "30", NowUtc.AddDays(-1))));

        var dto = await ByKeyUseCase(queries).ExecuteAsync(HoldKey, NowUtc.AddDays(-2));

        Assert.Equal("20", dto.Value);
        Assert.Equal(NowUtc.AddDays(-5), dto.EffectiveFromUtc);
    }

    [Theory]
    [InlineData("booking_hold_minutes")]
    [InlineData("  Booking_Hold_Minutes  ")]
    public async Task Config_by_key_ignores_case_and_surrounding_spaces(string key)
    {
        var queries = new FakeQueries(Config(1, HoldKey, "15"));

        var dto = await ByKeyUseCase(queries).ExecuteAsync(key);

        Assert.Equal(HoldKey, dto.Key);
        Assert.Equal(HoldKey, queries.RequestedKey);
    }

    [Fact]
    public async Task Config_by_unknown_key_throws_not_found()
    {
        var queries = new FakeQueries(Config(1, HoldKey, "15"));

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => ByKeyUseCase(queries).ExecuteAsync("hold_ttl"));

        Assert.Contains("Tham số hệ thống", ex.Message);
        Assert.Contains("HOLD_TTL", ex.Message);
    }

    private static GetPlatformSettingsUseCase ListUseCase(FakeQueries queries) => new(queries, new FixedTimeProvider(Now));

    private static GetSystemConfigByKeyUseCase ByKeyUseCase(FakeQueries queries) => new(queries, new FixedTimeProvider(Now));

    private static SystemConfigRecord Config(int id, string key, string defaultValue, params SystemConfigChangePoint[] changes)
        => new(id, key, defaultValue, "int", "Mô tả", changes);

    private static SystemConfigChangePoint Change(int id, string value, DateTime effectiveFromUtc, DateTime? cancelledAtUtc = null)
        => new(id, value, effectiveFromUtc, cancelledAtUtc);

    /// <summary>
    /// Trả đủ mọi dòng lịch sử (kể cả thay đổi chưa tới hạn) để chứng minh giá trị hiệu lực do Application tính,
    /// không dựa vào việc truy vấn đã lọc sẵn.
    /// </summary>
    private sealed class FakeQueries(params SystemConfigRecord[] configs) : ISettingsQueries
    {
        public DateTime? ChangesUpToUtc { get; private set; }
        public string? RequestedKey { get; private set; }

        public Task<IReadOnlyList<SystemConfigRecord>> ListConfigsAsync(DateTime changesUpToUtc, CancellationToken cancellationToken)
        {
            ChangesUpToUtc = changesUpToUtc;
            return Task.FromResult<IReadOnlyList<SystemConfigRecord>>(configs);
        }

        // Khớp chính xác như so sánh chuỗi của PostgreSQL.
        public Task<SystemConfigRecord?> FindConfigByKeyAsync(string key, DateTime changesUpToUtc, CancellationToken cancellationToken)
        {
            RequestedKey = key;
            ChangesUpToUtc = changesUpToUtc;
            return Task.FromResult(configs.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal)));
        }

        public Task<IReadOnlyList<FeatureFlagDto>> ListFeatureFlagsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<FeatureFlagDto>>([new("FEATURE_3D_MAP", false, null)]);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
