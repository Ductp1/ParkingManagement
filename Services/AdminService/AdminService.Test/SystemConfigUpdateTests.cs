using System.Text.Json;
using AdminService.Application.Features;
using AdminService.Application.Features.Settings;
using AdminService.Domain.Entities;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-098: đổi giá trị tham số có Effective Date (unit test với fake repository, không cần database).
public class SystemConfigUpdateTests
{
    private const string HoldKey = "BOOKING_HOLD_MINUTES";
    private const string CommissionKey = "DEFAULT_COMMISSION_RATE";
    private const string FloorKey = "PRICE_FLOOR_PER_HOUR_VND";
    private const string CeilingKey = "PRICE_CEILING_PER_HOUR_VND";
    private const string GraceKey = "CHECKIN_GRACE_PERIOD_MINUTES";
    private const string NoShowKey = "NO_SHOW_CANCEL_AFTER_MINUTES";
    private const int AdminUser = 1;

    // Lệch 250 ms để kiểm tra mốc hiệu lực được cắt về giây.
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 8, 0, 0, 250, TimeSpan.Zero);
    private static readonly DateTime NowUtc = Now.UtcDateTime;
    private static readonly DateTime NowSecond = new(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc);

    private static readonly UpdateSystemConfigRequest ValidRequest = new("20", "  Giảm tải giờ cao điểm  ");

    // ===== Thành công =====

    [Fact]
    public async Task Change_without_effective_date_applies_now_and_is_saved_with_audit_log()
    {
        var repo = new FakeRepo(Config(1, HoldKey, "15", "int"));

        var dto = await CreateUseCase(repo).ExecuteAsync(HoldKey, ValidRequest, AdminUser);

        var change = Assert.Single(repo.Changes);
        Assert.Equal(1, change.SystemConfigId);
        Assert.Equal("20", change.Value);
        Assert.Equal(NowSecond, change.EffectiveFromUtc);                 // hiệu lực ngay, cắt về giây
        Assert.Equal(DateTimeKind.Utc, change.EffectiveFromUtc.Kind);
        Assert.Equal("Giảm tải giờ cao điểm", change.Reason);            // trim
        Assert.Equal(AdminUser, change.CreatedByUserId);
        Assert.Null(change.CancelledAtUtc);

        var audit = Assert.Single(repo.AuditLogs);
        Assert.Equal("AdminService", audit.SourceService);
        Assert.Equal(AdminUser, audit.UserId);
        Assert.Null(audit.OwnerProfileId);
        Assert.Equal("SystemConfig.ChangeScheduled", audit.Action);
        Assert.Equal("SystemConfig", audit.EntityName);
        Assert.Equal(HoldKey, audit.EntityId);
        Assert.Equal("Giảm tải giờ cao điểm", audit.Reason);
        using (var oldValues = JsonDocument.Parse(audit.OldValuesJson!))
            Assert.Equal("15", oldValues.RootElement.GetProperty("value").GetString());
        using (var newValues = JsonDocument.Parse(audit.NewValuesJson!))
        {
            Assert.Equal("20", newValues.RootElement.GetProperty("value").GetString());
            Assert.Equal(NowSecond, newValues.RootElement.GetProperty("effectiveFromUtc").GetDateTime().ToUniversalTime());
        }

        Assert.Equal(1, repo.AddChangeCount);                             // thay đổi + audit log đi chung 1 lần ghi
        Assert.Equal(FakeRepo.GeneratedId, dto.Id);                       // Id do database sinh khi lưu
        Assert.Equal(HoldKey, dto.Key);
        Assert.Equal("20", dto.Value);
        Assert.Equal("15", dto.PreviousValue);
        Assert.Equal(NowSecond, dto.EffectiveFromUtc);
        Assert.Equal("Effective", dto.Status);
        Assert.Equal("Giảm tải giờ cao điểm", dto.Reason);
        Assert.Equal(AdminUser, dto.CreatedByUserId);
        Assert.Null(dto.CancelledAtUtc);
        Assert.Null(dto.CancelledByUserId);
        Assert.Null(dto.CancelReason);
    }

    [Fact]
    public async Task Change_with_future_effective_date_is_scheduled()
    {
        var repo = new FakeRepo(Config(1, HoldKey, "15", "int"));
        var effectiveFrom = NowSecond.AddDays(3);

        var dto = await CreateUseCase(repo).ExecuteAsync(HoldKey, ValidRequest with { EffectiveFromUtc = effectiveFrom }, AdminUser);

        Assert.Equal(effectiveFrom, repo.Changes.Single().EffectiveFromUtc);
        Assert.Equal("Scheduled", dto.Status);
        Assert.Equal("15", dto.PreviousValue);
        using var newValues = JsonDocument.Parse(repo.AuditLogs.Single().NewValuesJson!);
        Assert.Equal(effectiveFrom, newValues.RootElement.GetProperty("effectiveFromUtc").GetDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task Previous_value_is_the_one_in_effect_at_the_effective_date_including_scheduled_changes()
    {
        var repo = new FakeRepo(Config(1, HoldKey, "15", "int", Change(1, "25", NowSecond.AddDays(2))));

        var dto = await CreateUseCase(repo).ExecuteAsync(HoldKey, ValidRequest with { EffectiveFromUtc = NowSecond.AddDays(5) }, AdminUser);

        Assert.Equal("25", dto.PreviousValue);
        using var oldValues = JsonDocument.Parse(repo.AuditLogs.Single().OldValuesJson!);
        Assert.Equal("25", oldValues.RootElement.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Effective_date_without_time_zone_is_read_as_utc_and_truncated_to_seconds()
    {
        var repo = new FakeRepo(Config(1, HoldKey, "15", "int"));
        var unspecified = DateTime.SpecifyKind(NowSecond.AddDays(1).AddMilliseconds(789), DateTimeKind.Unspecified);

        await CreateUseCase(repo).ExecuteAsync(HoldKey, ValidRequest with { EffectiveFromUtc = unspecified }, AdminUser);

        var effectiveFrom = repo.Changes.Single().EffectiveFromUtc;
        Assert.Equal(DateTimeKind.Utc, effectiveFrom.Kind);               // timestamptz chỉ nhận UTC
        Assert.Equal(NowSecond.AddDays(1), effectiveFrom);
    }

    [Fact]
    public async Task Effective_date_inside_the_current_second_counts_as_now()
    {
        var repo = new FakeRepo(Config(1, HoldKey, "15", "int"));

        var dto = await CreateUseCase(repo).ExecuteAsync(HoldKey, ValidRequest with { EffectiveFromUtc = NowSecond }, AdminUser);

        Assert.Equal(NowSecond, repo.Changes.Single().EffectiveFromUtc);
        Assert.Equal("Effective", dto.Status);
    }

    [Fact]
    public async Task Effective_date_of_exactly_365_days_is_accepted()
    {
        var repo = new FakeRepo(Config(1, HoldKey, "15", "int"));

        await CreateUseCase(repo).ExecuteAsync(HoldKey, ValidRequest with { EffectiveFromUtc = NowUtc.AddDays(365) }, AdminUser);

        Assert.Equal(NowSecond.AddDays(365), repo.Changes.Single().EffectiveFromUtc);
    }

    [Fact]
    public async Task Key_is_matched_ignoring_case_and_value_is_normalized()
    {
        var repo = new FakeRepo(Config(1, CommissionKey, "0.10", "decimal"));

        var dto = await CreateUseCase(repo).ExecuteAsync(" default_commission_rate ", new("  0.15 ", "Điều chỉnh hoa hồng"), AdminUser);

        Assert.Equal(CommissionKey, dto.Key);
        Assert.Equal("0.15", repo.Changes.Single().Value);
        Assert.Equal(CommissionKey, repo.AuditLogs.Single().EntityId);
    }

    [Fact]
    public async Task Effective_date_of_a_cancelled_change_can_be_used_again()
    {
        var effectiveFrom = NowSecond.AddDays(3);
        var repo = new FakeRepo(Config(1, HoldKey, "15", "int", Change(1, "30", effectiveFrom, cancelledAtUtc: NowSecond.AddHours(-1))));

        await CreateUseCase(repo).ExecuteAsync(HoldKey, ValidRequest with { EffectiveFromUtc = effectiveFrom }, AdminUser);

        Assert.Equal(effectiveFrom, repo.Changes.Single().EffectiveFromUtc);
    }

    // ===== 400 =====

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_reason_is_rejected(string? reason)
    {
        var repo = new FakeRepo(Config(1, HoldKey, "15", "int"));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase(repo).ExecuteAsync(HoldKey, ValidRequest with { Reason = reason }, AdminUser));

        Assert.Equal("Lý do thay đổi không được để trống.", ex.Message);
        AssertNothingSaved(repo);
    }

    [Fact]
    public async Task Reason_longer_than_1000_characters_is_rejected_but_1000_is_accepted()
    {
        var repo = new FakeRepo(Config(1, HoldKey, "15", "int"));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase(repo).ExecuteAsync(HoldKey, ValidRequest with { Reason = new string('a', 1001) }, AdminUser));
        Assert.Equal("Lý do thay đổi tối đa 1000 ký tự.", ex.Message);
        AssertNothingSaved(repo);

        await CreateUseCase(repo).ExecuteAsync(HoldKey, ValidRequest with { Reason = new string('a', 1000) }, AdminUser);
        Assert.Equal(1000, repo.Changes.Single().Reason.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Invalid_acting_admin_is_rejected(int performedByUserId)
    {
        var repo = new FakeRepo(Config(1, HoldKey, "15", "int"));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => CreateUseCase(repo).ExecuteAsync(HoldKey, ValidRequest, performedByUserId));

        Assert.Equal("performedByUserId phải là số nguyên dương.", ex.Message);
        AssertNothingSaved(repo);
    }

    [Fact]
    public async Task Effective_date_in_the_past_is_rejected()
    {
        var repo = new FakeRepo(Config(1, HoldKey, "15", "int"));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase(repo).ExecuteAsync(HoldKey, ValidRequest with { EffectiveFromUtc = NowSecond.AddSeconds(-1) }, AdminUser));

        Assert.Equal("effectiveFromUtc không được ở quá khứ.", ex.Message);
        AssertNothingSaved(repo);
    }

    [Fact]
    public async Task Effective_date_more_than_365_days_ahead_is_rejected()
    {
        var repo = new FakeRepo(Config(1, HoldKey, "15", "int"));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase(repo).ExecuteAsync(HoldKey, ValidRequest with { EffectiveFromUtc = NowUtc.AddDays(365).AddSeconds(1) }, AdminUser));

        Assert.Equal("effectiveFromUtc không được xa hơn 365 ngày kể từ hiện tại.", ex.Message);
        AssertNothingSaved(repo);
    }

    [Theory]
    [InlineData(null, "Giá trị tham số không được để trống.")]
    [InlineData("abc", "Giá trị của BOOKING_HOLD_MINUTES phải là số nguyên.")]
    [InlineData("0", "Giá trị của BOOKING_HOLD_MINUTES phải trong khoảng 1–120.")]
    [InlineData("121", "Giá trị của BOOKING_HOLD_MINUTES phải trong khoảng 1–120.")]
    public async Task Value_breaking_the_rule_of_its_key_is_rejected(string? value, string expectedMessage)
    {
        var repo = new FakeRepo(Config(1, HoldKey, "15", "int"));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase(repo).ExecuteAsync(HoldKey, ValidRequest with { Value = value }, AdminUser));

        Assert.Equal(expectedMessage, ex.Message);
        AssertNothingSaved(repo);
    }

    // ===== 404 =====

    [Fact]
    public async Task Unknown_key_throws_not_found()
    {
        var repo = new FakeRepo(Config(1, HoldKey, "15", "int"));

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => CreateUseCase(repo).ExecuteAsync("hold_ttl", ValidRequest, AdminUser));

        Assert.Contains("Tham số hệ thống", ex.Message);
        Assert.Contains("HOLD_TTL", ex.Message);
        AssertNothingSaved(repo);
    }

    // ===== 409 =====

    [Fact]
    public async Task Second_change_on_the_same_effective_date_is_a_conflict()
    {
        var effectiveFrom = NowSecond.AddDays(3);
        var repo = new FakeRepo(Config(1, HoldKey, "15", "int", Change(1, "30", effectiveFrom)));

        // Lệch vài trăm ms vẫn là cùng một mốc sau khi cắt về giây.
        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => CreateUseCase(repo).ExecuteAsync(HoldKey, ValidRequest with { EffectiveFromUtc = effectiveFrom.AddMilliseconds(400) }, AdminUser));

        Assert.Equal("Tham số BOOKING_HOLD_MINUTES đã có một thay đổi hiệu lực từ 2026-10-12T08:00:00Z.", ex.Message);
        AssertNothingSaved(repo);
    }

    [Theory]
    [InlineData("int", "15", "15")]
    [InlineData("int", "15", "015")]
    [InlineData("decimal", "0.10", "0.1")]
    public async Task Value_equal_to_the_one_already_in_effect_is_a_conflict(string dataType, string currentValue, string newValue)
    {
        var key = dataType == "int" ? HoldKey : CommissionKey;
        var repo = new FakeRepo(Config(1, key, currentValue, dataType));

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => CreateUseCase(repo).ExecuteAsync(key, ValidRequest with { Value = newValue }, AdminUser));

        Assert.Equal($"Giá trị mới trùng với giá trị của {key} đang hiệu lực tại 2026-10-09T08:00:00Z.", ex.Message);
        AssertNothingSaved(repo);
    }

    [Fact]
    public async Task Value_equal_to_default_is_allowed_when_another_value_is_in_effect_at_the_effective_date()
    {
        // 15 (gốc) → 25 từ ngày +2. Đặt lại 15 từ ngày +5 là hợp lệ vì lúc đó đang là 25.
        var repo = new FakeRepo(Config(1, HoldKey, "15", "int", Change(1, "25", NowSecond.AddDays(2))));

        var dto = await CreateUseCase(repo).ExecuteAsync(HoldKey, new("15", "Trả về mặc định", NowSecond.AddDays(5)), AdminUser);

        Assert.Equal("15", dto.Value);
        Assert.Equal("25", dto.PreviousValue);
    }

    [Fact]
    public async Task Concurrent_change_stopped_by_the_unique_index_surfaces_as_conflict()
    {
        var repo = new FakeRepo(Config(1, HoldKey, "15", "int")) { AddChangeFailure = new ConflictException("trùng mốc hiệu lực") };

        await Assert.ThrowsAsync<ConflictException>(() => CreateUseCase(repo).ExecuteAsync(HoldKey, ValidRequest, AdminUser));

        AssertNothingSaved(repo);
    }

    // ===== Quy tắc liên khóa =====

    [Fact]
    public async Task Price_floor_above_ceiling_is_rejected()
    {
        var repo = new FakeRepo(Config(1, FloorKey, "5000", "decimal"), Config(2, CeilingKey, "200000", "decimal"));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase(repo).ExecuteAsync(FloorKey, new("250000", "Tăng sàn giá"), AdminUser));

        Assert.Equal("Sàn giá (PRICE_FLOOR_PER_HOUR_VND = 250000) không được lớn hơn trần giá (PRICE_CEILING_PER_HOUR_VND = 200000) " +
            "kể từ 2026-10-09T08:00:00Z.", ex.Message);
        AssertNothingSaved(repo);
    }

    [Fact]
    public async Task Price_ceiling_below_floor_is_rejected()
    {
        var repo = new FakeRepo(Config(1, FloorKey, "5000", "decimal"), Config(2, CeilingKey, "200000", "decimal"));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase(repo).ExecuteAsync(CeilingKey, new("4000", "Hạ trần giá"), AdminUser));

        Assert.Equal("Sàn giá (PRICE_FLOOR_PER_HOUR_VND = 5000) không được lớn hơn trần giá (PRICE_CEILING_PER_HOUR_VND = 4000) " +
            "kể từ 2026-10-09T08:00:00Z.", ex.Message);
        AssertNothingSaved(repo);
    }

    [Fact]
    public async Task Price_floor_equal_to_ceiling_is_accepted()
    {
        var repo = new FakeRepo(Config(1, FloorKey, "5000", "decimal"), Config(2, CeilingKey, "200000", "decimal"));

        await CreateUseCase(repo).ExecuteAsync(FloorKey, new("200000", "Giá cố định"), AdminUser);

        Assert.Equal("200000", repo.Changes.Single().Value);
    }

    [Fact]
    public async Task Order_rule_uses_the_other_value_in_effect_at_the_effective_date_not_the_current_one()
    {
        // Trần hiện là 200000 nhưng đã lên lịch tăng lên 500000 từ ngày +2. Sàn 300000 từ ngày +5 là hợp lệ.
        var repo = new FakeRepo(
            Config(1, FloorKey, "5000", "decimal"),
            Config(2, CeilingKey, "200000", "decimal", Change(1, "500000", NowSecond.AddDays(2))));

        await CreateUseCase(repo).ExecuteAsync(FloorKey, new("300000", "Tăng sàn giá", NowSecond.AddDays(5)), AdminUser);

        Assert.Equal("300000", repo.Changes.Single().Value);

        // Cùng giá trị nhưng hiệu lực ngay thì vi phạm: lúc này trần vẫn là 200000.
        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase(repo).ExecuteAsync(FloorKey, new("300000", "Tăng sàn giá"), AdminUser));
        Assert.Contains("PRICE_CEILING_PER_HOUR_VND = 200000", ex.Message);
        Assert.Contains("kể từ 2026-10-09T08:00:00Z", ex.Message);
    }

    [Fact]
    public async Task Order_rule_also_checks_changes_of_the_other_key_scheduled_after_the_effective_date()
    {
        // Trần 200000 sẽ hạ xuống 100000 từ ngày +5. Sàn 150000 từ ngày +1 hợp lệ lúc đầu nhưng vi phạm từ ngày +5.
        var repo = new FakeRepo(
            Config(1, FloorKey, "5000", "decimal"),
            Config(2, CeilingKey, "200000", "decimal", Change(1, "100000", NowSecond.AddDays(5))));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase(repo).ExecuteAsync(FloorKey, new("150000", "Tăng sàn giá", NowSecond.AddDays(1)), AdminUser));

        Assert.Equal("Sàn giá (PRICE_FLOOR_PER_HOUR_VND = 150000) không được lớn hơn trần giá (PRICE_CEILING_PER_HOUR_VND = 100000) " +
            "kể từ 2026-10-14T08:00:00Z.", ex.Message);
        AssertNothingSaved(repo);
    }

    [Fact]
    public async Task Order_rule_stops_checking_at_the_next_scheduled_change_of_the_same_key()
    {
        // Sàn sẽ về 4000 từ ngày +3, trần hạ xuống 100000 từ ngày +5: sàn 150000 chỉ áp dụng trong ngày +1..+3 nên hợp lệ.
        var repo = new FakeRepo(
            Config(1, FloorKey, "5000", "decimal", Change(1, "4000", NowSecond.AddDays(3))),
            Config(2, CeilingKey, "200000", "decimal", Change(2, "100000", NowSecond.AddDays(5))));

        await CreateUseCase(repo).ExecuteAsync(FloorKey, new("150000", "Tăng sàn giá tạm thời", NowSecond.AddDays(1)), AdminUser);

        Assert.Equal("150000", repo.Changes.Single().Value);
    }

    [Fact]
    public async Task Order_rule_ignores_cancelled_changes_of_the_other_key()
    {
        var repo = new FakeRepo(
            Config(1, FloorKey, "5000", "decimal"),
            Config(2, CeilingKey, "200000", "decimal", Change(1, "100000", NowSecond.AddDays(5), cancelledAtUtc: NowSecond.AddHours(-1))));

        await CreateUseCase(repo).ExecuteAsync(FloorKey, new("150000", "Tăng sàn giá", NowSecond.AddDays(1)), AdminUser);

        Assert.Equal("150000", repo.Changes.Single().Value);
    }

    [Fact]
    public async Task No_show_cancel_mark_below_checkin_grace_is_rejected()
    {
        var repo = new FakeRepo(Config(1, GraceKey, "15", "int"), Config(2, NoShowKey, "30", "int"));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase(repo).ExecuteAsync(NoShowKey, new("10", "Hủy no-show sớm hơn"), AdminUser));

        Assert.Equal("Ân hạn check-in (CHECKIN_GRACE_PERIOD_MINUTES = 15) không được lớn hơn mốc hủy no-show " +
            "(NO_SHOW_CANCEL_AFTER_MINUTES = 10) kể từ 2026-10-09T08:00:00Z.", ex.Message);
        AssertNothingSaved(repo);
    }

    [Fact]
    public async Task Checkin_grace_above_no_show_cancel_mark_is_rejected_and_equal_is_accepted()
    {
        var repo = new FakeRepo(Config(1, GraceKey, "15", "int"), Config(2, NoShowKey, "30", "int"));

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase(repo).ExecuteAsync(GraceKey, new("45", "Nới ân hạn"), AdminUser));
        Assert.Contains("CHECKIN_GRACE_PERIOD_MINUTES = 45", ex.Message);
        Assert.Contains("NO_SHOW_CANCEL_AFTER_MINUTES = 30", ex.Message);
        AssertNothingSaved(repo);

        await CreateUseCase(repo).ExecuteAsync(GraceKey, new("30", "Nới ân hạn"), AdminUser);
        Assert.Equal("30", repo.Changes.Single().Value);
    }

    [Fact]
    public async Task Key_without_order_rule_does_not_read_any_other_key()
    {
        var repo = new FakeRepo(Config(1, HoldKey, "15", "int"));

        await CreateUseCase(repo).ExecuteAsync(HoldKey, ValidRequest, AdminUser);

        Assert.Equal([HoldKey], repo.FindCalls);
    }

    private static void AssertNothingSaved(FakeRepo repo)
    {
        Assert.Empty(repo.Changes);
        Assert.Empty(repo.AuditLogs);
        Assert.Equal(0, repo.AddChangeCount);
    }

    private static UpdateSystemConfigUseCase CreateUseCase(FakeRepo repo) => new(repo, new FixedTimeProvider(Now));

    private static SystemConfigRecord Config(int id, string key, string defaultValue, string dataType, params SystemConfigChangePoint[] changes)
        => new(id, key, defaultValue, dataType, "Mô tả", changes);

    private static SystemConfigChangePoint Change(int id, string value, DateTime effectiveFromUtc, DateTime? cancelledAtUtc = null)
        => new(id, value, effectiveFromUtc, cancelledAtUtc);

    private sealed class FakeRepo(params SystemConfigRecord[] configs) : ISystemConfigRepository
    {
        public const int GeneratedId = 77;

        public List<SystemConfigChange> Changes { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];
        public List<string> FindCalls { get; } = [];
        public int AddChangeCount { get; private set; }
        public Exception? AddChangeFailure { get; init; }

        // Khớp chính xác như so sánh chuỗi của PostgreSQL.
        public Task<SystemConfigRecord?> FindByKeyWithChangesAsync(string key, CancellationToken cancellationToken)
        {
            FindCalls.Add(key);
            return Task.FromResult(configs.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal)));
        }

        public Task AddChangeAsync(SystemConfigChange change, AuditLog auditLog, CancellationToken cancellationToken)
        {
            if (AddChangeFailure is not null) throw AddChangeFailure;
            AddChangeCount++;
            // Database sinh Id khi SaveChanges.
            typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(change, GeneratedId);
            Changes.Add(change);
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
