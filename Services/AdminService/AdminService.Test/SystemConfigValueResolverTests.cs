using AdminService.Application.Features.Settings;

namespace AdminService.Test;

// US-098: giá trị hiệu lực của tham số theo thời điểm (hàm thuần, không cần database).
public class SystemConfigValueResolverTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void No_change_returns_default_value_without_effective_date()
    {
        var result = SystemConfigValueResolver.Resolve("15", [], Now);

        Assert.Equal("15", result.Value);
        Assert.Null(result.EffectiveFromUtc);
    }

    [Fact]
    public void Change_applies_from_its_effective_date_inclusive()
    {
        SystemConfigChangePoint[] changes = [Change(1, "20", Now)];

        Assert.Equal("15", SystemConfigValueResolver.Resolve("15", changes, Now.AddTicks(-1)).Value);

        var atEffectiveDate = SystemConfigValueResolver.Resolve("15", changes, Now);
        Assert.Equal("20", atEffectiveDate.Value);
        Assert.Equal(Now, atEffectiveDate.EffectiveFromUtc);
    }

    [Fact]
    public void Scheduled_change_is_ignored_until_its_effective_date()
    {
        SystemConfigChangePoint[] changes = [Change(1, "20", Now.AddDays(-2)), Change(2, "30", Now.AddDays(3))];

        Assert.Equal("20", SystemConfigValueResolver.Resolve("15", changes, Now).Value);
        Assert.Equal("30", SystemConfigValueResolver.Resolve("15", changes, Now.AddDays(3)).Value);
    }

    [Fact]
    public void Latest_effective_change_wins_whatever_the_input_order()
    {
        SystemConfigChangePoint[] changes =
        [
            Change(3, "30", Now.AddDays(-1)),
            Change(1, "20", Now.AddDays(-5)),
            Change(2, "25", Now.AddDays(-3)),
        ];

        var result = SystemConfigValueResolver.Resolve("15", changes, Now);

        Assert.Equal("30", result.Value);
        Assert.Equal(Now.AddDays(-1), result.EffectiveFromUtc);
    }

    [Fact]
    public void Past_moment_keeps_the_value_that_was_in_effect_then()
    {
        // AC2: booking tạo lúc T đọc lại tham số tại T vẫn ra giá trị cũ dù sau đó Admin đã đổi.
        SystemConfigChangePoint[] changes = [Change(1, "20", Now.AddDays(-5)), Change(2, "30", Now.AddDays(-1))];

        Assert.Equal("15", SystemConfigValueResolver.Resolve("15", changes, Now.AddDays(-6)).Value);
        Assert.Equal("20", SystemConfigValueResolver.Resolve("15", changes, Now.AddDays(-2)).Value);
        Assert.Equal("30", SystemConfigValueResolver.Resolve("15", changes, Now).Value);
    }

    [Fact]
    public void Cancelled_change_is_skipped_and_the_previous_value_stays()
    {
        SystemConfigChangePoint[] changes =
        [
            Change(1, "20", Now.AddDays(-5)),
            Change(2, "30", Now.AddDays(-1), cancelledAtUtc: Now.AddDays(-2)),
        ];

        var result = SystemConfigValueResolver.Resolve("15", changes, Now);

        Assert.Equal("20", result.Value);
        Assert.Equal(Now.AddDays(-5), result.EffectiveFromUtc);
    }

    [Fact]
    public void Only_cancelled_changes_fall_back_to_default_value()
    {
        SystemConfigChangePoint[] changes = [Change(1, "20", Now.AddDays(-1), cancelledAtUtc: Now.AddDays(-2))];

        var result = SystemConfigValueResolver.Resolve("15", changes, Now);

        Assert.Equal("15", result.Value);
        Assert.Null(result.EffectiveFromUtc);
    }

    [Fact]
    public void Change_rescheduled_on_a_cancelled_effective_date_is_used()
    {
        // Unique index chỉ tính thay đổi còn giá trị: mốc của thay đổi đã hủy được đặt lại.
        SystemConfigChangePoint[] changes =
        [
            Change(1, "20", Now.AddDays(-1), cancelledAtUtc: Now.AddDays(-2)),
            Change(2, "25", Now.AddDays(-1)),
        ];

        Assert.Equal("25", SystemConfigValueResolver.Resolve("15", changes, Now).Value);
    }

    private static SystemConfigChangePoint Change(int id, string value, DateTime effectiveFromUtc, DateTime? cancelledAtUtc = null)
        => new(id, value, effectiveFromUtc, cancelledAtUtc);
}
