using System.Text.Json;
using AdminService.Application.Features;
using AdminService.Application.Features.Settings;
using AdminService.Domain.Entities;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-098: hủy thay đổi tham số chưa tới ngày hiệu lực (unit test với fake repository, không cần database).
public class SystemConfigCancelTests
{
    private const string HoldKey = "BOOKING_HOLD_MINUTES";
    private const string FloorKey = "PRICE_FLOOR_PER_HOUR_VND";
    private const string CeilingKey = "PRICE_CEILING_PER_HOUR_VND";
    private const int HoldConfigId = 1;
    private const int AdminUser = 1;
    private const int OtherAdmin = 9;

    private static readonly DateTimeOffset Now = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTime NowUtc = Now.UtcDateTime;

    private static readonly CancelSystemConfigChangeRequest ValidRequest = new("  Nhập nhầm giá trị  ");

    // ===== Thành công =====

    [Fact]
    public async Task Cancel_scheduled_change_stamps_it_and_saves_with_audit_log()
    {
        var scheduled = Entity(5, HoldConfigId, "30", NowUtc.AddDays(3), createdBy: OtherAdmin);
        var repo = new FakeRepo([Config(HoldConfigId, HoldKey, "15", "int", scheduled)], scheduled);

        var dto = await CreateUseCase(repo).ExecuteAsync(HoldKey, 5, ValidRequest, AdminUser);

        Assert.Equal(NowUtc, scheduled.CancelledAtUtc);
        Assert.Equal(AdminUser, scheduled.CancelledByUserId);
        Assert.Equal("Nhập nhầm giá trị", scheduled.CancelReason);       // trim
        Assert.Equal("30", scheduled.Value);                             // giá trị và người tạo không bị sửa
        Assert.Equal(NowUtc.AddDays(3), scheduled.EffectiveFromUtc);
        Assert.Equal(OtherAdmin, scheduled.CreatedByUserId);
        Assert.Equal("Lý do đổi", scheduled.Reason);

        var audit = Assert.Single(repo.AuditLogs);
        Assert.Equal("AdminService", audit.SourceService);
        Assert.Equal(AdminUser, audit.UserId);                           // người hủy, không phải người tạo
        Assert.Equal("SystemConfig.ChangeCancelled", audit.Action);
        Assert.Equal("SystemConfig", audit.EntityName);
        Assert.Equal(HoldKey, audit.EntityId);
        Assert.Equal("Nhập nhầm giá trị", audit.Reason);
        using (var oldValues = JsonDocument.Parse(audit.OldValuesJson!))
        {
            Assert.Equal(5, oldValues.RootElement.GetProperty("changeId").GetInt32());
            Assert.Equal("30", oldValues.RootElement.GetProperty("value").GetString());
            Assert.Equal(NowUtc.AddDays(3), oldValues.RootElement.GetProperty("effectiveFromUtc").GetDateTime().ToUniversalTime());
        }
        using (var newValues = JsonDocument.Parse(audit.NewValuesJson!))
        {
            Assert.Equal(5, newValues.RootElement.GetProperty("changeId").GetInt32());
            Assert.True(newValues.RootElement.GetProperty("cancelled").GetBoolean());
            Assert.Equal("15", newValues.RootElement.GetProperty("value").GetString());
        }

        Assert.Equal(1, repo.SaveWithAuditCount);                        // dấu hủy + audit log đi chung 1 lần ghi
        Assert.Equal(5, dto.Id);
        Assert.Equal(HoldKey, dto.Key);
        Assert.Equal("30", dto.Value);
        Assert.Equal("15", dto.PreviousValue);
        Assert.Equal("Cancelled", dto.Status);
        Assert.Equal("Lý do đổi", dto.Reason);
        Assert.Equal(OtherAdmin, dto.CreatedByUserId);
        Assert.Equal(NowUtc, dto.CancelledAtUtc);
        Assert.Equal(AdminUser, dto.CancelledByUserId);
        Assert.Equal("Nhập nhầm giá trị", dto.CancelReason);
    }

    [Fact]
    public async Task Cancel_restores_the_value_of_the_previous_change_at_that_date()
    {
        var earlier = Entity(4, HoldConfigId, "25", NowUtc.AddDays(1));
        var scheduled = Entity(5, HoldConfigId, "30", NowUtc.AddDays(3));
        var repo = new FakeRepo([Config(HoldConfigId, HoldKey, "15", "int", earlier, scheduled)], earlier, scheduled);

        var dto = await CreateUseCase(repo).ExecuteAsync(HoldKey, 5, ValidRequest, AdminUser);

        Assert.Equal("25", dto.PreviousValue);
        using var newValues = JsonDocument.Parse(repo.AuditLogs.Single().NewValuesJson!);
        Assert.Equal("25", newValues.RootElement.GetProperty("value").GetString());
        Assert.Null(earlier.CancelledAtUtc);                             // thay đổi khác không bị đụng tới
    }

    [Fact]
    public async Task Key_is_matched_ignoring_case_and_surrounding_spaces()
    {
        var scheduled = Entity(5, HoldConfigId, "30", NowUtc.AddDays(3));
        var repo = new FakeRepo([Config(HoldConfigId, HoldKey, "15", "int", scheduled)], scheduled);

        var dto = await CreateUseCase(repo).ExecuteAsync(" booking_hold_minutes ", 5, ValidRequest, AdminUser);

        Assert.Equal(HoldKey, dto.Key);
        Assert.Equal("Cancelled", dto.Status);
    }

    // ===== 400 =====

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_reason_is_rejected(string? reason)
    {
        var scheduled = Entity(5, HoldConfigId, "30", NowUtc.AddDays(3));
        var repo = new FakeRepo([Config(HoldConfigId, HoldKey, "15", "int", scheduled)], scheduled);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => CreateUseCase(repo).ExecuteAsync(HoldKey, 5, new(reason), AdminUser));

        Assert.Equal("Lý do hủy không được để trống.", ex.Message);
        AssertNothingSaved(repo, scheduled);
    }

    [Fact]
    public async Task Reason_longer_than_1000_characters_is_rejected_but_1000_is_accepted()
    {
        var scheduled = Entity(5, HoldConfigId, "30", NowUtc.AddDays(3));
        var repo = new FakeRepo([Config(HoldConfigId, HoldKey, "15", "int", scheduled)], scheduled);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase(repo).ExecuteAsync(HoldKey, 5, new(new string('a', 1001)), AdminUser));
        Assert.Equal("Lý do hủy tối đa 1000 ký tự.", ex.Message);
        AssertNothingSaved(repo, scheduled);

        await CreateUseCase(repo).ExecuteAsync(HoldKey, 5, new(new string('a', 1000)), AdminUser);
        Assert.Equal(1000, scheduled.CancelReason!.Length);
    }

    [Theory]
    [InlineData(0, 1, "changeId phải là số nguyên dương.")]
    [InlineData(-3, 1, "changeId phải là số nguyên dương.")]
    [InlineData(5, 0, "performedByUserId phải là số nguyên dương.")]
    public async Task Invalid_ids_are_rejected(int changeId, int performedByUserId, string expectedMessage)
    {
        var scheduled = Entity(5, HoldConfigId, "30", NowUtc.AddDays(3));
        var repo = new FakeRepo([Config(HoldConfigId, HoldKey, "15", "int", scheduled)], scheduled);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase(repo).ExecuteAsync(HoldKey, changeId, ValidRequest, performedByUserId));

        Assert.Equal(expectedMessage, ex.Message);
        AssertNothingSaved(repo, scheduled);
    }

    // ===== 404 =====

    [Fact]
    public async Task Unknown_key_throws_not_found()
    {
        var scheduled = Entity(5, HoldConfigId, "30", NowUtc.AddDays(3));
        var repo = new FakeRepo([Config(HoldConfigId, HoldKey, "15", "int", scheduled)], scheduled);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => CreateUseCase(repo).ExecuteAsync("hold_ttl", 5, ValidRequest, AdminUser));

        Assert.Contains("Tham số hệ thống", ex.Message);
        Assert.Contains("HOLD_TTL", ex.Message);
        AssertNothingSaved(repo, scheduled);
    }

    [Fact]
    public async Task Unknown_change_throws_not_found()
    {
        var scheduled = Entity(5, HoldConfigId, "30", NowUtc.AddDays(3));
        var repo = new FakeRepo([Config(HoldConfigId, HoldKey, "15", "int", scheduled)], scheduled);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => CreateUseCase(repo).ExecuteAsync(HoldKey, 999, ValidRequest, AdminUser));

        Assert.Contains("Thay đổi của tham số BOOKING_HOLD_MINUTES", ex.Message);
        Assert.Contains("999", ex.Message);
        AssertNothingSaved(repo, scheduled);
    }

    [Fact]
    public async Task Change_of_another_key_throws_not_found()
    {
        // Thay đổi #5 thuộc sàn giá; gọi hủy dưới khóa BOOKING_HOLD_MINUTES thì coi như không có.
        var floorChange = Entity(5, systemConfigId: 2, "6000", NowUtc.AddDays(3));
        var repo = new FakeRepo(
            [Config(HoldConfigId, HoldKey, "15", "int"), Config(2, FloorKey, "5000", "decimal", floorChange), Config(3, CeilingKey, "200000", "decimal")],
            floorChange);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => CreateUseCase(repo).ExecuteAsync(HoldKey, 5, ValidRequest, AdminUser));

        Assert.Contains("Thay đổi của tham số BOOKING_HOLD_MINUTES", ex.Message);
        AssertNothingSaved(repo, floorChange);
    }

    // ===== 409 =====

    [Fact]
    public async Task Change_already_in_effect_cannot_be_cancelled()
    {
        var effective = Entity(5, HoldConfigId, "30", NowUtc.AddDays(-1));
        var repo = new FakeRepo([Config(HoldConfigId, HoldKey, "15", "int", effective)], effective);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => CreateUseCase(repo).ExecuteAsync(HoldKey, 5, ValidRequest, AdminUser));

        Assert.Equal("Thay đổi #5 đã có hiệu lực từ 2026-10-08T08:00:00Z nên không hủy được.", ex.Message);
        AssertNothingSaved(repo, effective);
    }

    [Fact]
    public async Task Change_taking_effect_exactly_now_cannot_be_cancelled()
    {
        var effective = Entity(5, HoldConfigId, "30", NowUtc);
        var repo = new FakeRepo([Config(HoldConfigId, HoldKey, "15", "int", effective)], effective);

        await Assert.ThrowsAsync<ConflictException>(() => CreateUseCase(repo).ExecuteAsync(HoldKey, 5, ValidRequest, AdminUser));

        AssertNothingSaved(repo, effective);
    }

    [Fact]
    public async Task Change_already_cancelled_cannot_be_cancelled_again()
    {
        var cancelled = Entity(5, HoldConfigId, "30", NowUtc.AddDays(3));
        cancelled.Cancel(OtherAdmin, "Hủy lần đầu", NowUtc.AddHours(-1));
        var repo = new FakeRepo([Config(HoldConfigId, HoldKey, "15", "int", cancelled)], cancelled);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => CreateUseCase(repo).ExecuteAsync(HoldKey, 5, ValidRequest, AdminUser));

        Assert.Equal("Thay đổi #5 đã bị hủy trước đó.", ex.Message);
        Assert.Equal(OtherAdmin, cancelled.CancelledByUserId);           // dấu hủy cũ giữ nguyên
        Assert.Equal("Hủy lần đầu", cancelled.CancelReason);
        Assert.Equal(NowUtc.AddHours(-1), cancelled.CancelledAtUtc);
        Assert.Empty(repo.AuditLogs);
        Assert.Equal(0, repo.SaveWithAuditCount);
    }

    // ===== Quy tắc liên khóa =====

    [Fact]
    public async Task Cancel_that_would_leave_price_floor_above_ceiling_is_a_conflict()
    {
        // Trần 200000 → 500000 từ ngày +2; sàn 5000 → 300000 từ ngày +5 (hợp lệ nhờ trần mới).
        // Hủy thay đổi của trần thì từ ngày +5 sàn 300000 > trần 200000.
        var ceilingChange = Entity(7, systemConfigId: 3, "500000", NowUtc.AddDays(2));
        var floorChange = Entity(8, systemConfigId: 2, "300000", NowUtc.AddDays(5));
        var repo = new FakeRepo(
            [Config(2, FloorKey, "5000", "decimal", floorChange), Config(3, CeilingKey, "200000", "decimal", ceilingChange)],
            ceilingChange, floorChange);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => CreateUseCase(repo).ExecuteAsync(CeilingKey, 7, ValidRequest, AdminUser));

        Assert.Equal("Không thể hủy thay đổi #7: Sàn giá (PRICE_FLOOR_PER_HOUR_VND = 300000) không được lớn hơn trần giá " +
            "(PRICE_CEILING_PER_HOUR_VND = 200000) kể từ 2026-10-14T08:00:00Z.", ex.Message);
        AssertNothingSaved(repo, ceilingChange);
    }

    [Fact]
    public async Task Cancel_is_allowed_when_the_restored_value_still_satisfies_the_order_rule()
    {
        var ceilingChange = Entity(7, systemConfigId: 3, "500000", NowUtc.AddDays(2));
        var floorChange = Entity(8, systemConfigId: 2, "150000", NowUtc.AddDays(5));
        var repo = new FakeRepo(
            [Config(2, FloorKey, "5000", "decimal", floorChange), Config(3, CeilingKey, "200000", "decimal", ceilingChange)],
            ceilingChange, floorChange);

        var dto = await CreateUseCase(repo).ExecuteAsync(CeilingKey, 7, ValidRequest, AdminUser);

        Assert.Equal("Cancelled", dto.Status);
        Assert.Equal("200000", dto.PreviousValue);
        Assert.NotNull(ceilingChange.CancelledAtUtc);
    }

    [Fact]
    public async Task Cancel_only_checks_the_order_rule_until_the_next_change_of_the_same_key()
    {
        // Trần: 200000 → 500000 (ngày +2, sẽ hủy) → 600000 (ngày +4). Sàn 300000 từ ngày +5 nằm sau mốc +4 nên không bị ảnh hưởng.
        var ceilingChange = Entity(7, systemConfigId: 3, "500000", NowUtc.AddDays(2));
        var laterCeiling = Entity(9, systemConfigId: 3, "600000", NowUtc.AddDays(4));
        var floorChange = Entity(8, systemConfigId: 2, "300000", NowUtc.AddDays(5));
        var repo = new FakeRepo(
            [Config(2, FloorKey, "5000", "decimal", floorChange), Config(3, CeilingKey, "200000", "decimal", ceilingChange, laterCeiling)],
            ceilingChange, laterCeiling, floorChange);

        var dto = await CreateUseCase(repo).ExecuteAsync(CeilingKey, 7, ValidRequest, AdminUser);

        Assert.Equal("Cancelled", dto.Status);
    }

    private static void AssertNothingSaved(FakeRepo repo, SystemConfigChange change)
    {
        Assert.Null(change.CancelledAtUtc);
        Assert.Null(change.CancelledByUserId);
        Assert.Null(change.CancelReason);
        Assert.Empty(repo.AuditLogs);
        Assert.Equal(0, repo.SaveWithAuditCount);
    }

    private static CancelSystemConfigChangeUseCase CreateUseCase(FakeRepo repo) => new(repo, new FixedTimeProvider(Now));

    private static SystemConfigChange Entity(int id, int systemConfigId, string value, DateTime effectiveFromUtc, int createdBy = AdminUser)
    {
        var change = SystemConfigChange.Schedule(systemConfigId, value, effectiveFromUtc, "Lý do đổi", createdBy);
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(change, id);    // Id do database sinh
        return change;
    }

    /// <summary>Tham số kèm lịch sử, chụp từ trạng thái hiện tại của các entity – giống dữ liệu đọc lên từ pm_admin.</summary>
    private static Func<SystemConfigRecord> Config(int id, string key, string defaultValue, string dataType, params SystemConfigChange[] changes)
        => () => new(id, key, defaultValue, dataType, "Mô tả",
            [.. changes.Select(c => new SystemConfigChangePoint(c.Id, c.Value, c.EffectiveFromUtc, c.CancelledAtUtc))]);

    private sealed class FakeRepo(Func<SystemConfigRecord>[] configs, params SystemConfigChange[] changes) : ISystemConfigRepository
    {
        public List<AuditLog> AuditLogs { get; } = [];
        public int SaveWithAuditCount { get; private set; }

        // Khớp chính xác như so sánh chuỗi của PostgreSQL.
        public Task<SystemConfigRecord?> FindByKeyWithChangesAsync(string key, CancellationToken cancellationToken)
            => Task.FromResult(configs.Select(c => c()).FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal)));

        public Task<SystemConfigChange?> FindChangeTrackedAsync(int changeId, CancellationToken cancellationToken)
            => Task.FromResult(changes.FirstOrDefault(c => c.Id == changeId));

        public Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            SaveWithAuditCount++;
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        // Hủy thay đổi không thêm dòng lịch sử mới.
        public Task AddChangeAsync(SystemConfigChange change, AuditLog auditLog, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
