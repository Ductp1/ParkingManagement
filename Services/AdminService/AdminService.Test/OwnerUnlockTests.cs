using System.Text.Json;
using AdminService.Application.Features.Owners;
using AdminService.Application.Features.Users;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-096: mở khóa tài khoản chủ bãi (unit test với fake IUserServiceClient + fake repository, không cần UserService hay database).
public class OwnerUnlockTests
{
    private const int OwnerTsn = 2;         // OwnerProfileId
    private const int AdminUser = 1;
    private const int OtherAdmin = 7;

    private static readonly DateTimeOffset Now = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTime NowUtc = Now.UtcDateTime;

    private static readonly UnlockOwnerRequest ValidRequest = new("  Đã khắc phục vi phạm  ", OtherAdmin);

    [Fact]
    public async Task Unlock_revokes_active_lock_with_audit_log_then_syncs()
    {
        var sanction = SyncedLock(lockedUntilUtc: null);
        var repo = new FakeRepo(sanction);
        var userService = new FakeUserService();
        // Lúc UserService được gọi, chế tài đã phải được gỡ và audit log đã ghi trong pm_admin.
        userService.OnUnlock = () =>
        {
            Assert.Equal(SanctionStatus.Revoked, sanction.Status);
            Assert.Equal(SanctionSyncStatus.Pending, sanction.UserServiceSyncStatus);
            Assert.Null(sanction.UserServiceSyncedAtUtc);
            Assert.Single(repo.AuditLogs);
        };

        var dto = await CreateUseCase(userService, repo).ExecuteAsync(OwnerTsn, ValidRequest);

        Assert.Equal(SanctionStatus.Revoked, sanction.Status);
        Assert.Equal(SanctionSyncStatus.Synced, sanction.UserServiceSyncStatus);
        Assert.Equal(NowUtc, sanction.UserServiceSyncedAtUtc);
        Assert.Equal("Gian lận doanh thu", sanction.Reason);            // lý do khóa giữ nguyên
        Assert.Equal(AdminUser, sanction.IssuedByUserId);

        var audit = Assert.Single(repo.AuditLogs);
        Assert.Equal("AdminService", audit.SourceService);
        Assert.Equal(OtherAdmin, audit.UserId);                          // người mở khóa, không phải người đã khóa
        Assert.Equal(OwnerTsn, audit.OwnerProfileId);
        Assert.Equal("Owner.Unlocked", audit.Action);
        Assert.Equal("OwnerProfile", audit.EntityName);
        Assert.Equal("2", audit.EntityId);
        Assert.Equal("Đã khắc phục vi phạm", audit.Reason);             // lý do mở khóa (trim) chỉ nằm ở audit log
        using (var oldValues = JsonDocument.Parse(audit.OldValuesJson!))
        {
            Assert.True(oldValues.RootElement.GetProperty("isLocked").GetBoolean());
            Assert.Equal("PermanentBan", oldValues.RootElement.GetProperty("level").GetString());
            Assert.Equal(JsonValueKind.Null, oldValues.RootElement.GetProperty("lockedUntilUtc").ValueKind);
            Assert.Equal(sanction.Id, oldValues.RootElement.GetProperty("sanctionId").GetInt32());
        }
        using (var newValues = JsonDocument.Parse(audit.NewValuesJson!))
            Assert.False(newValues.RootElement.GetProperty("isLocked").GetBoolean());

        Assert.Equal(1, repo.SaveWithAuditCount);                        // gỡ chế tài + audit log đi chung 1 lần ghi
        Assert.Equal(1, repo.SaveCount);                                 // chỉ để ghi kết quả đồng bộ
        Assert.Equal((OwnerTsn, "Đã khắc phục vi phạm", OtherAdmin), Assert.Single(userService.UnlockCalls));
        Assert.Equal("Revoked", dto.Status);
        Assert.Equal("Synced", dto.UserServiceSyncStatus);
        Assert.Equal(NowUtc, dto.UserServiceSyncedAtUtc);
    }

    [Fact]
    public async Task Unlock_audit_log_keeps_the_lock_window_that_was_lifted()
    {
        var lockedUntil = NowUtc.AddDays(5);
        var repo = new FakeRepo(SyncedLock(lockedUntil));

        await CreateUseCase(new FakeUserService(), repo).ExecuteAsync(OwnerTsn, ValidRequest);

        using var oldValues = JsonDocument.Parse(repo.AuditLogs.Single().OldValuesJson!);
        Assert.Equal("TemporarySuspension", oldValues.RootElement.GetProperty("level").GetString());
        Assert.Equal(lockedUntil, oldValues.RootElement.GetProperty("lockedUntilUtc").GetDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task Unlock_lifts_a_lock_that_ran_out_but_was_never_closed()
    {
        var sanction = SyncedLock(lockedUntilUtc: NowUtc.AddDays(-1));   // quá hạn nhưng vẫn Active
        var repo = new FakeRepo(sanction);

        var dto = await CreateUseCase(new FakeUserService(), repo).ExecuteAsync(OwnerTsn, ValidRequest);

        Assert.Equal(SanctionStatus.Revoked, sanction.Status);
        Assert.Equal("Synced", dto.UserServiceSyncStatus);
    }

    [Fact]
    public async Task Unlock_accepts_reason_of_exactly_1000_characters()
    {
        var repo = new FakeRepo(SyncedLock(null));

        await CreateUseCase(new FakeUserService(), repo).ExecuteAsync(OwnerTsn, ValidRequest with { Reason = new string('a', 1000) });

        Assert.Equal(1000, repo.AuditLogs.Single().Reason!.Length);
    }

    [Fact]
    public async Task User_service_failure_keeps_sanction_revoked_and_marks_sync_failed()
    {
        var sanction = SyncedLock(null);
        var repo = new FakeRepo(sanction);
        var userService = new FakeUserService { UnlockFailure = new UserServiceUnavailableException("down") };

        var dto = await CreateUseCase(userService, repo).ExecuteAsync(OwnerTsn, ValidRequest);   // không ném lỗi ra ngoài

        Assert.Equal(SanctionStatus.Revoked, sanction.Status);
        Assert.Equal(SanctionSyncStatus.Failed, sanction.UserServiceSyncStatus);
        Assert.Null(sanction.UserServiceSyncedAtUtc);
        Assert.Single(repo.AuditLogs);
        Assert.Equal(1, repo.SaveCount);
        Assert.Equal("Failed", dto.UserServiceSyncStatus);
    }

    [Fact]
    public async Task Owner_missing_in_user_service_at_unlock_time_marks_sync_failed()
    {
        var sanction = SyncedLock(null);
        var userService = new FakeUserService { UnlockReturnsNull = true };

        var dto = await CreateUseCase(userService, new FakeRepo(sanction)).ExecuteAsync(OwnerTsn, ValidRequest);

        Assert.Equal(SanctionSyncStatus.Failed, sanction.UserServiceSyncStatus);
        Assert.Equal("Failed", dto.UserServiceSyncStatus);
    }

    [Theory]
    [InlineData(SanctionSyncStatus.Failed)]
    [InlineData(SanctionSyncStatus.Pending)]
    public async Task Unlock_again_while_not_synced_retries_without_writing_another_audit_log(SanctionSyncStatus currentStatus)
    {
        var sanction = SyncedLock(null);
        sanction.Revoke();
        sanction.UserServiceSyncStatus = currentStatus;
        var repo = new FakeRepo(sanction);
        var userService = new FakeUserService();

        var dto = await CreateUseCase(userService, repo).ExecuteAsync(OwnerTsn, ValidRequest);

        Assert.Empty(repo.AuditLogs);                                    // lần mở khóa đầu đã ghi audit log rồi
        Assert.Equal(0, repo.SaveWithAuditCount);
        Assert.Equal(1, repo.SaveCount);
        // Lý do mở khóa không lưu trên chế tài nên lần thử lại dùng dữ liệu của chính request này.
        Assert.Equal((OwnerTsn, "Đã khắc phục vi phạm", OtherAdmin), Assert.Single(userService.UnlockCalls));
        Assert.Equal(SanctionStatus.Revoked, sanction.Status);
        Assert.Equal(SanctionSyncStatus.Synced, sanction.UserServiceSyncStatus);
        Assert.Equal("Synced", dto.UserServiceSyncStatus);
    }

    [Fact]
    public async Task Unlock_owner_that_was_never_locked_conflicts()
        => await AssertNotLocked(new FakeRepo());

    [Fact]
    public async Task Unlock_owner_already_unlocked_and_synced_conflicts()
    {
        var sanction = SyncedLock(null);
        sanction.Revoke();
        sanction.MarkSynced(NowUtc.AddHours(-1));

        await AssertNotLocked(new FakeRepo(sanction));
    }

    [Fact]
    public async Task Unlock_owner_whose_lock_already_expired_conflicts()
    {
        var sanction = SyncedLock(NowUtc.AddDays(-1));
        sanction.Expire();

        await AssertNotLocked(new FakeRepo(sanction));
    }

    [Fact]
    public async Task Unlock_owner_with_only_a_lot_level_sanction_conflicts()
    {
        // Chế tài áp lên một bãi (ParkingLotId có giá trị) không phải là khóa tài khoản chủ bãi.
        var lotSanction = new Sanction
        {
            OwnerProfileId = OwnerTsn, ParkingLotId = 4, Level = SanctionLevel.TemporarySuspension, Reason = "Overbooking",
            StartsAtUtc = NowUtc.AddDays(-2), EndsAtUtc = NowUtc.AddDays(12), IssuedByUserId = AdminUser
        };

        await AssertNotLocked(new FakeRepo(lotSanction));
        Assert.Equal(SanctionStatus.Active, lotSanction.Status);         // không bị đụng tới
    }

    [Fact]
    public async Task Unlock_looks_at_the_latest_lock_only_and_ignores_stale_unsynced_ones()
    {
        // Lần mở khóa cũ chưa đồng bộ được, sau đó chủ bãi bị khóa lại rồi mở khóa xong → hiện không bị khóa.
        var stale = SyncedLock(null);
        stale.Revoke();
        stale.MarkSyncFailed();
        var latest = SyncedLock(null);
        latest.Revoke();
        latest.MarkSynced(NowUtc.AddMinutes(-5));
        var userService = new FakeUserService();

        await AssertNotLocked(new FakeRepo(stale, latest), userService);

        Assert.Equal(SanctionSyncStatus.Failed, stale.UserServiceSyncStatus);   // không bị gửi lại
    }

    [Fact]
    public async Task Unlock_revokes_the_current_lock_and_leaves_older_sanctions_alone()
    {
        var older = SyncedLock(null);
        older.Revoke();
        older.MarkSynced(NowUtc.AddDays(-3));
        var current = SyncedLock(null);
        var repo = new FakeRepo(older, current);

        await CreateUseCase(new FakeUserService(), repo).ExecuteAsync(OwnerTsn, ValidRequest);

        Assert.Equal(SanctionStatus.Revoked, current.Status);
        Assert.Equal(NowUtc, current.UserServiceSyncedAtUtc);
        Assert.Equal(NowUtc.AddDays(-3), older.UserServiceSyncedAtUtc);
        Assert.Single(repo.AuditLogs);
    }

    [Theory]
    [InlineData(0, "Đã khắc phục", 1, "ownerProfileId phải là số nguyên dương.")]
    [InlineData(-1, "Đã khắc phục", 1, "ownerProfileId phải là số nguyên dương.")]
    [InlineData(2, "Đã khắc phục", 0, "performedByUserId phải là số nguyên dương.")]
    [InlineData(2, null, 1, "Lý do mở khóa không được để trống.")]
    [InlineData(2, "", 1, "Lý do mở khóa không được để trống.")]
    [InlineData(2, "   ", 1, "Lý do mở khóa không được để trống.")]
    public async Task Unlock_with_invalid_input_is_rejected(int ownerProfileId, string? reason, int performedByUserId, string expectedMessage)
    {
        var sanction = SyncedLock(null);
        var repo = new FakeRepo(sanction);
        var userService = new FakeUserService();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => CreateUseCase(userService, repo)
            .ExecuteAsync(ownerProfileId, new UnlockOwnerRequest(reason, performedByUserId)));

        Assert.Equal(expectedMessage, ex.Message);
        AssertNothingHappened(userService, repo);
        Assert.Equal(0, repo.FindCount);                                 // sai đầu vào thì không truy vấn
        Assert.Equal(SanctionStatus.Active, sanction.Status);
    }

    [Fact]
    public async Task Unlock_with_reason_longer_than_1000_characters_is_rejected()
    {
        var repo = new FakeRepo(SyncedLock(null));
        var userService = new FakeUserService();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => CreateUseCase(userService, repo)
            .ExecuteAsync(OwnerTsn, ValidRequest with { Reason = new string('a', 1001) }));

        Assert.Equal("Lý do mở khóa tối đa 1000 ký tự.", ex.Message);
        AssertNothingHappened(userService, repo);
    }

    [Fact]
    public void Revoke_puts_sanction_back_to_waiting_for_user_service()
    {
        var sanction = SyncedLock(null);

        sanction.Revoke();

        Assert.Equal(SanctionStatus.Revoked, sanction.Status);
        Assert.Equal(SanctionSyncStatus.Pending, sanction.UserServiceSyncStatus);
        Assert.Null(sanction.UserServiceSyncedAtUtc);                    // mốc đồng bộ của lệnh khóa không còn đúng
        Assert.False(sanction.IsInEffect(NowUtc));
    }

    /// <summary>Chế tài khóa chủ bãi đang Active và UserService đã nhận lệnh khóa.</summary>
    private static Sanction SyncedLock(DateTime? lockedUntilUtc)
    {
        var sanction = Sanction.CreateOwnerLock(OwnerTsn, "Gian lận doanh thu", AdminUser, NowUtc.AddDays(-10), lockedUntilUtc);
        sanction.MarkSynced(NowUtc.AddDays(-10));
        return sanction;
    }

    private static UnlockOwnerUseCase CreateUseCase(FakeUserService userService, FakeRepo repo)
        => new(userService, repo, new FixedTimeProvider(Now), NullLogger<UnlockOwnerUseCase>.Instance);

    private static async Task AssertNotLocked(FakeRepo repo, FakeUserService? userService = null)
    {
        userService ??= new FakeUserService();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => CreateUseCase(userService, repo).ExecuteAsync(OwnerTsn, ValidRequest));

        Assert.Equal("Chủ bãi 2 hiện không bị khóa.", ex.Message);
        AssertNothingHappened(userService, repo);
    }

    private static void AssertNothingHappened(FakeUserService userService, FakeRepo repo)
    {
        Assert.Empty(userService.UnlockCalls);
        Assert.Empty(repo.AuditLogs);
        Assert.Equal(0, repo.SaveWithAuditCount);
        Assert.Equal(0, repo.SaveCount);
    }

    private sealed class FakeUserService : IUserServiceClient
    {
        public List<(int OwnerProfileId, string Reason, int PerformedByUserId)> UnlockCalls { get; } = [];
        public Exception? UnlockFailure { get; init; }
        public bool UnlockReturnsNull { get; init; }
        public Action? OnUnlock { get; set; }

        public Task<OwnerAccountDto?> UnlockOwnerAsync(int ownerProfileId, string reason, int performedByUserId, CancellationToken cancellationToken)
        {
            UnlockCalls.Add((ownerProfileId, reason, performedByUserId));
            OnUnlock?.Invoke();
            if (UnlockFailure is not null) throw UnlockFailure;
            return Task.FromResult(UnlockReturnsNull
                ? null
                : new OwnerAccountDto(ownerProfileId, 3, "Công ty CP Bãi xe Tân Sơn Nhất", "Công ty Bãi xe Tân Sơn Nhất", null, null, false, "Active", "Active"));
        }

        // Mở khóa không xác minh lại chủ bãi và không gọi lệnh khóa.
        public Task<PagedResult<AdminUserSummaryDto>> ListUsersAsync(UserRoleType? role, int page, int pageSize, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<AdminUserDto?> FindUserByIdAsync(int userId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<OwnerAccountDto?> LockOwnerAsync(int ownerProfileId, string reason, DateTime? lockedUntilUtc, int performedByUserId, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class FakeRepo(params Sanction[] existing) : IOwnerLockRepository
    {
        /// <summary>Thứ tự trong danh sách = thứ tự tạo (phần tử cuối là mới nhất), vì BaseEntity.Id của fake luôn là 0.</summary>
        public List<Sanction> Sanctions { get; } = [.. existing];
        public List<AuditLog> AuditLogs { get; } = [];
        public int FindCount { get; private set; }
        public int SaveWithAuditCount { get; private set; }
        public int SaveCount { get; private set; }

        public Task<Sanction?> FindLatestOwnerLockTrackedAsync(int ownerProfileId, CancellationToken cancellationToken)
        {
            FindCount++;
            return Task.FromResult(Sanctions.LastOrDefault(s => s.OwnerProfileId == ownerProfileId
                && s.ParkingLotId is null && (s.Level is SanctionLevel.TemporarySuspension or SanctionLevel.PermanentBan)));
        }

        public Task SaveWithAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            SaveWithAuditCount++;
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        // Mở khóa không dùng các thao tác của luồng khóa.
        public Task<Sanction?> FindActiveOwnerLockTrackedAsync(int ownerProfileId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task AddLockAsync(Sanction sanction, AuditLog auditLog, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
