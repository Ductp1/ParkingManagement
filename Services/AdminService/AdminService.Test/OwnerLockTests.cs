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

// US-096: khóa tài khoản chủ bãi (unit test với fake IUserServiceClient + fake repository, không cần UserService hay database).
public class OwnerLockTests
{
    private const int OwnerTsn = 2;         // OwnerProfileId
    private const int OwnerTsnUser = 3;     // user sở hữu hồ sơ OwnerTsn
    private const int AdminUser = 1;

    private static readonly DateTimeOffset Now = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTime NowUtc = Now.UtcDateTime;

    private static readonly AdminUserDto TsnOwner =
        new(OwnerTsnUser, "Công ty Bãi xe Tân Sơn Nhất", "owner.tsn@smartparking.vn", "0900000003", "Active", "NotSubmitted", ["LotOwner"],
            new AdminUserOwnerProfileDto(OwnerTsn, "Công ty CP Bãi xe Tân Sơn Nhất"));
    private static readonly AdminUserDto VincomOwner =
        new(2, "Công ty Vincom Parking", "owner.vincom@smartparking.vn", "0900000002", "Active", "NotSubmitted", ["LotOwner", "Driver"],
            new AdminUserOwnerProfileDto(1, "Công ty TNHH Vincom Parking"));
    private static readonly AdminUserDto Driver =
        new(5, "Nguyễn Văn An", "driver1@smartparking.vn", "0900000005", "Active", "Verified", ["Driver"], null);

    private static readonly LockOwnerRequest ValidRequest = new(OwnerTsnUser, "  Gian lận doanh thu  ", AdminUser);

    [Fact]
    public async Task Lock_without_end_date_saves_permanent_ban_with_audit_log_then_syncs()
    {
        var repo = new FakeRepo();
        var userService = new FakeUserService(TsnOwner);
        // Lúc UserService được gọi, chế tài đã phải nằm trong pm_admin ở trạng thái chờ đồng bộ.
        userService.OnLock = () =>
        {
            Assert.Equal(SanctionSyncStatus.Pending, Assert.Single(repo.Sanctions).UserServiceSyncStatus);
            Assert.Single(repo.AuditLogs);
        };

        var dto = await CreateUseCase(userService, repo).ExecuteAsync(OwnerTsn, ValidRequest);

        var sanction = Assert.Single(repo.Sanctions);
        Assert.Equal(OwnerTsn, sanction.OwnerProfileId);
        Assert.Null(sanction.ParkingLotId);                              // áp lên toàn bộ chủ bãi
        Assert.Equal(SanctionLevel.PermanentBan, sanction.Level);
        Assert.Equal("Gian lận doanh thu", sanction.Reason);            // trim
        Assert.Equal(NowUtc, sanction.StartsAtUtc);
        Assert.Null(sanction.EndsAtUtc);
        Assert.Equal(SanctionStatus.Active, sanction.Status);
        Assert.Equal(AdminUser, sanction.IssuedByUserId);
        Assert.Null(sanction.EvidenceJson);
        Assert.Equal(SanctionSyncStatus.Synced, sanction.UserServiceSyncStatus);
        Assert.Equal(NowUtc, sanction.UserServiceSyncedAtUtc);

        var audit = Assert.Single(repo.AuditLogs);
        Assert.Equal("AdminService", audit.SourceService);
        Assert.Equal(AdminUser, audit.UserId);
        Assert.Equal(OwnerTsn, audit.OwnerProfileId);
        Assert.Equal("Owner.Locked", audit.Action);
        Assert.Equal("OwnerProfile", audit.EntityName);
        Assert.Equal("2", audit.EntityId);
        Assert.Equal("Gian lận doanh thu", audit.Reason);
        using (var oldValues = JsonDocument.Parse(audit.OldValuesJson!))
            Assert.False(oldValues.RootElement.GetProperty("isLocked").GetBoolean());
        using (var newValues = JsonDocument.Parse(audit.NewValuesJson!))
        {
            Assert.True(newValues.RootElement.GetProperty("isLocked").GetBoolean());
            Assert.Equal(OwnerTsnUser, newValues.RootElement.GetProperty("ownerUserId").GetInt32());
            Assert.Equal("PermanentBan", newValues.RootElement.GetProperty("level").GetString());
            Assert.Equal(JsonValueKind.Null, newValues.RootElement.GetProperty("lockedUntilUtc").ValueKind);
        }

        Assert.Equal(1, repo.AddLockCount);                              // chế tài + audit log đi chung 1 lần ghi
        Assert.Equal(1, repo.SaveCount);                                 // chỉ để ghi kết quả đồng bộ
        Assert.Equal((OwnerTsn, "Gian lận doanh thu", (DateTime?)null, AdminUser), Assert.Single(userService.LockCalls));
        Assert.Equal("PermanentBan", dto.Level);
        Assert.Equal("Active", dto.Status);
        Assert.Equal("Synced", dto.UserServiceSyncStatus);
        Assert.Equal(NowUtc, dto.UserServiceSyncedAtUtc);
    }

    [Fact]
    public async Task Lock_with_end_date_saves_temporary_suspension_until_that_time()
    {
        var repo = new FakeRepo();
        var userService = new FakeUserService(TsnOwner);
        var lockedUntil = NowUtc.AddDays(7);

        var dto = await CreateUseCase(userService, repo).ExecuteAsync(OwnerTsn, ValidRequest with { LockedUntilUtc = lockedUntil });

        var sanction = Assert.Single(repo.Sanctions);
        Assert.Equal(SanctionLevel.TemporarySuspension, sanction.Level);
        Assert.Equal(lockedUntil, sanction.EndsAtUtc);
        Assert.Equal(lockedUntil, Assert.Single(userService.LockCalls).LockedUntilUtc);
        Assert.Equal("TemporarySuspension", dto.Level);
        using var newValues = JsonDocument.Parse(repo.AuditLogs.Single().NewValuesJson!);
        Assert.Equal(lockedUntil, newValues.RootElement.GetProperty("lockedUntilUtc").GetDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task Lock_end_date_without_time_zone_is_read_as_utc()
    {
        var repo = new FakeRepo();
        var unspecified = DateTime.SpecifyKind(NowUtc.AddDays(1), DateTimeKind.Unspecified);

        await CreateUseCase(new FakeUserService(TsnOwner), repo).ExecuteAsync(OwnerTsn, ValidRequest with { LockedUntilUtc = unspecified });

        var endsAt = repo.Sanctions.Single().EndsAtUtc!.Value;
        Assert.Equal(DateTimeKind.Utc, endsAt.Kind);                     // timestamptz chỉ nhận UTC
        Assert.Equal(unspecified.Ticks, endsAt.Ticks);
    }

    [Fact]
    public async Task Lock_accepts_reason_of_exactly_1000_characters()
    {
        var repo = new FakeRepo();

        await CreateUseCase(new FakeUserService(TsnOwner), repo).ExecuteAsync(OwnerTsn, ValidRequest with { Reason = new string('a', 1000) });

        Assert.Equal(1000, repo.Sanctions.Single().Reason.Length);
    }

    [Fact]
    public async Task User_service_failure_keeps_sanction_and_marks_sync_failed()
    {
        var repo = new FakeRepo();
        var userService = new FakeUserService(TsnOwner) { LockFailure = new UserServiceUnavailableException("down") };

        var dto = await CreateUseCase(userService, repo).ExecuteAsync(OwnerTsn, ValidRequest);   // không ném lỗi ra ngoài

        var sanction = Assert.Single(repo.Sanctions);
        Assert.Equal(SanctionStatus.Active, sanction.Status);
        Assert.Equal(SanctionSyncStatus.Failed, sanction.UserServiceSyncStatus);
        Assert.Null(sanction.UserServiceSyncedAtUtc);
        Assert.Single(repo.AuditLogs);                                   // audit log vẫn còn: thao tác của admin đã được ghi nhận
        Assert.Equal(1, repo.SaveCount);
        Assert.Equal("Failed", dto.UserServiceSyncStatus);
    }

    [Fact]
    public async Task Owner_missing_in_user_service_at_lock_time_marks_sync_failed()
    {
        var repo = new FakeRepo();
        var userService = new FakeUserService(TsnOwner) { LockReturnsNull = true };

        var dto = await CreateUseCase(userService, repo).ExecuteAsync(OwnerTsn, ValidRequest);

        Assert.Equal(SanctionSyncStatus.Failed, repo.Sanctions.Single().UserServiceSyncStatus);
        Assert.Equal("Failed", dto.UserServiceSyncStatus);
    }

    [Theory]
    [InlineData(SanctionSyncStatus.Failed)]
    [InlineData(SanctionSyncStatus.Pending)]
    public async Task Lock_again_while_not_synced_retries_existing_sanction_without_duplicating(SanctionSyncStatus currentStatus)
    {
        var existing = Sanction.CreateOwnerLock(OwnerTsn, "Lý do ban đầu", issuedByUserId: 9, NowUtc.AddHours(-1), NowUtc.AddDays(3));
        existing.UserServiceSyncStatus = currentStatus;
        var repo = new FakeRepo(existing);
        var userService = new FakeUserService(TsnOwner);

        var dto = await CreateUseCase(userService, repo).ExecuteAsync(OwnerTsn, ValidRequest);

        Assert.Same(existing, Assert.Single(repo.Sanctions));            // không tạo chế tài trùng
        Assert.Empty(repo.AuditLogs);                                    // không ghi thêm audit log cho lần thử lại
        Assert.Equal(0, repo.AddLockCount);
        Assert.Equal(1, repo.SaveCount);
        // Gửi lại đúng dữ liệu đã lưu, không lấy lý do / người thao tác của lần gọi lại.
        Assert.Equal((OwnerTsn, "Lý do ban đầu", (DateTime?)NowUtc.AddDays(3), 9), Assert.Single(userService.LockCalls));
        Assert.Equal(SanctionSyncStatus.Synced, existing.UserServiceSyncStatus);
        Assert.Equal("Lý do ban đầu", dto.Reason);
        Assert.Equal("Synced", dto.UserServiceSyncStatus);
    }

    [Fact]
    public async Task Lock_owner_already_locked_and_synced_conflicts()
    {
        var existing = Sanction.CreateOwnerLock(OwnerTsn, "Đã khóa", AdminUser, NowUtc.AddHours(-1), null);
        existing.MarkSynced(NowUtc.AddHours(-1));
        var repo = new FakeRepo(existing);
        var userService = new FakeUserService(TsnOwner);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => CreateUseCase(userService, repo).ExecuteAsync(OwnerTsn, ValidRequest));

        Assert.Equal("Chủ bãi 2 đang bị khóa (chế tài #0).", ex.Message);   // BaseEntity.Id của fake luôn là 0
        Assert.Empty(userService.LockCalls);
        Assert.Equal(0, repo.AddLockCount);
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task Lock_after_previous_lock_ran_out_expires_it_and_creates_a_new_sanction()
    {
        var old = Sanction.CreateOwnerLock(OwnerTsn, "Tạm khóa cũ", AdminUser, NowUtc.AddDays(-10), NowUtc);   // hết hạn đúng lúc này
        old.MarkSynced(NowUtc.AddDays(-10));
        var repo = new FakeRepo(old);
        // Chế tài cũ phải được đóng trước khi chế tài mới được ghi (unique index chỉ cho 1 chế tài khóa Active).
        repo.OnAddLock = () => Assert.Equal(SanctionStatus.Expired, old.Status);

        var dto = await CreateUseCase(new FakeUserService(TsnOwner), repo).ExecuteAsync(OwnerTsn, ValidRequest);

        Assert.Equal(2, repo.Sanctions.Count);
        Assert.Equal(SanctionStatus.Expired, old.Status);
        var created = repo.Sanctions.Single(s => !ReferenceEquals(s, old));
        Assert.Equal(SanctionStatus.Active, created.Status);
        Assert.Equal("Gian lận doanh thu", created.Reason);
        Assert.Single(repo.AuditLogs);
        Assert.Equal("Synced", dto.UserServiceSyncStatus);
    }

    [Theory]
    [InlineData(0, 3, "Gian lận", 1, "ownerProfileId phải là số nguyên dương.")]
    [InlineData(-1, 3, "Gian lận", 1, "ownerProfileId phải là số nguyên dương.")]
    [InlineData(2, 0, "Gian lận", 1, "ownerUserId phải là số nguyên dương.")]
    [InlineData(2, 3, "Gian lận", 0, "performedByUserId phải là số nguyên dương.")]
    [InlineData(2, 3, null, 1, "Lý do khóa không được để trống.")]
    [InlineData(2, 3, "", 1, "Lý do khóa không được để trống.")]
    [InlineData(2, 3, "   ", 1, "Lý do khóa không được để trống.")]
    public async Task Lock_with_invalid_input_is_rejected(int ownerProfileId, int ownerUserId, string? reason, int performedByUserId, string expectedMessage)
    {
        var repo = new FakeRepo();
        var userService = new FakeUserService(TsnOwner);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => CreateUseCase(userService, repo)
            .ExecuteAsync(ownerProfileId, new LockOwnerRequest(ownerUserId, reason, performedByUserId)));

        Assert.Equal(expectedMessage, ex.Message);
        AssertNothingHappened(userService, repo, findCalls: 0);
    }

    [Fact]
    public async Task Lock_with_reason_longer_than_1000_characters_is_rejected()
    {
        var repo = new FakeRepo();
        var userService = new FakeUserService(TsnOwner);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => CreateUseCase(userService, repo)
            .ExecuteAsync(OwnerTsn, ValidRequest with { Reason = new string('a', 1001) }));

        Assert.Equal("Lý do khóa tối đa 1000 ký tự.", ex.Message);
        AssertNothingHappened(userService, repo, findCalls: 0);
    }

    [Theory]
    [InlineData(0)]         // đúng thời điểm hiện tại
    [InlineData(-60)]
    public async Task Lock_until_a_time_that_is_not_in_the_future_is_rejected(int secondsFromNow)
    {
        var repo = new FakeRepo();
        var userService = new FakeUserService(TsnOwner);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => CreateUseCase(userService, repo)
            .ExecuteAsync(OwnerTsn, ValidRequest with { LockedUntilUtc = NowUtc.AddSeconds(secondsFromNow) }));

        Assert.Equal("lockedUntilUtc phải sau thời điểm hiện tại.", ex.Message);
        AssertNothingHappened(userService, repo, findCalls: 0);
    }

    [Fact]
    public async Task Lock_for_non_existing_owner_user_is_not_found()
    {
        var repo = new FakeRepo();
        var userService = new FakeUserService(TsnOwner);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => CreateUseCase(userService, repo)
            .ExecuteAsync(OwnerTsn, ValidRequest with { OwnerUserId = 999 }));

        Assert.Equal("Người dùng với Id = '999' không tồn tại hoặc chưa được công khai.", ex.Message);
        AssertNothingHappened(userService, repo, findCalls: 1);
    }

    [Theory]
    [InlineData(2)]         // chủ bãi nhưng sở hữu hồ sơ khác (OwnerProfileId = 1)
    [InlineData(5)]         // tài xế, không có hồ sơ chủ bãi
    public async Task Lock_with_user_who_does_not_own_the_profile_is_rejected(int ownerUserId)
    {
        var repo = new FakeRepo();
        var userService = new FakeUserService(TsnOwner, VincomOwner, Driver);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => CreateUseCase(userService, repo)
            .ExecuteAsync(OwnerTsn, ValidRequest with { OwnerUserId = ownerUserId }));

        Assert.Equal($"Người dùng {ownerUserId} không sở hữu hồ sơ chủ bãi 2.", ex.Message);
        AssertNothingHappened(userService, repo, findCalls: 1);          // không lưu chế tài cho hồ sơ chưa xác minh
    }

    [Fact]
    public async Task User_service_down_before_saving_fails_without_writing_anything()
    {
        var repo = new FakeRepo();
        var userService = new FakeUserService(TsnOwner) { FindFailure = new UserServiceUnavailableException("down") };

        await Assert.ThrowsAsync<UserServiceUnavailableException>(() => CreateUseCase(userService, repo).ExecuteAsync(OwnerTsn, ValidRequest));

        AssertNothingHappened(userService, repo, findCalls: 1);
    }

    [Fact]
    public async Task Concurrent_lock_rejected_by_repository_does_not_call_user_service()
    {
        var repo = new FakeRepo { AddLockFailure = new ConflictException("Chủ bãi 2 đang được khóa bởi một yêu cầu khác.") };
        var userService = new FakeUserService(TsnOwner);

        await Assert.ThrowsAsync<ConflictException>(() => CreateUseCase(userService, repo).ExecuteAsync(OwnerTsn, ValidRequest));

        Assert.Empty(userService.LockCalls);
        Assert.Equal(0, repo.SaveCount);
    }

    [Theory]
    [InlineData(SanctionStatus.Active, null, true)]      // không hạn
    [InlineData(SanctionStatus.Active, 1, true)]         // còn 1 giây
    [InlineData(SanctionStatus.Active, 0, false)]        // hết hạn đúng lúc này
    [InlineData(SanctionStatus.Active, -1, false)]
    [InlineData(SanctionStatus.Revoked, null, false)]
    [InlineData(SanctionStatus.Expired, 1, false)]
    public void Sanction_is_in_effect_only_while_active_and_before_its_end(SanctionStatus status, int? endsInSeconds, bool expected)
    {
        var sanction = new Sanction { Status = status, EndsAtUtc = endsInSeconds is { } s ? NowUtc.AddSeconds(s) : null };

        Assert.Equal(expected, sanction.IsInEffect(NowUtc));
    }

    private static LockOwnerUseCase CreateUseCase(FakeUserService userService, FakeRepo repo)
        => new(userService, repo, new FixedTimeProvider(Now), NullLogger<LockOwnerUseCase>.Instance);

    private static void AssertNothingHappened(FakeUserService userService, FakeRepo repo, int findCalls)
    {
        Assert.Equal(findCalls, userService.FindCalls.Count);
        Assert.Empty(userService.LockCalls);
        Assert.Empty(repo.Sanctions);
        Assert.Empty(repo.AuditLogs);
        Assert.Equal(0, repo.SaveCount);
    }

    private sealed class FakeUserService(params AdminUserDto[] users) : IUserServiceClient
    {
        public List<int> FindCalls { get; } = [];
        public List<(int OwnerProfileId, string Reason, DateTime? LockedUntilUtc, int PerformedByUserId)> LockCalls { get; } = [];
        public Exception? FindFailure { get; init; }
        public Exception? LockFailure { get; init; }
        public bool LockReturnsNull { get; init; }
        public Action? OnLock { get; set; }

        public Task<AdminUserDto?> FindUserByIdAsync(int userId, CancellationToken cancellationToken)
        {
            FindCalls.Add(userId);
            if (FindFailure is not null) throw FindFailure;
            return Task.FromResult(users.FirstOrDefault(u => u.Id == userId));
        }

        public Task<OwnerAccountDto?> LockOwnerAsync(int ownerProfileId, string reason, DateTime? lockedUntilUtc, int performedByUserId, CancellationToken cancellationToken)
        {
            LockCalls.Add((ownerProfileId, reason, lockedUntilUtc, performedByUserId));
            OnLock?.Invoke();
            if (LockFailure is not null) throw LockFailure;
            return Task.FromResult(LockReturnsNull
                ? null
                : new OwnerAccountDto(ownerProfileId, OwnerTsnUser, "Công ty CP Bãi xe Tân Sơn Nhất", "Công ty Bãi xe Tân Sơn Nhất", null, null, true, "Suspended", "Active"));
        }

        // Khóa chủ bãi không đụng tới danh sách người dùng và lời gọi mở khóa.
        public Task<PagedResult<AdminUserSummaryDto>> ListUsersAsync(UserRoleType? role, int page, int pageSize, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<OwnerAccountDto?> UnlockOwnerAsync(int ownerProfileId, string reason, int performedByUserId, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class FakeRepo(params Sanction[] existing) : IOwnerLockRepository
    {
        public List<Sanction> Sanctions { get; } = [.. existing];
        public List<AuditLog> AuditLogs { get; } = [];
        public int AddLockCount { get; private set; }
        public int SaveCount { get; private set; }
        public Exception? AddLockFailure { get; init; }
        public Action? OnAddLock { get; set; }

        // Cùng điều kiện với unique index: Active + cấp chủ bãi + cấp tạm khóa / vĩnh viễn.
        public Task<Sanction?> FindActiveOwnerLockTrackedAsync(int ownerProfileId, CancellationToken cancellationToken)
            => Task.FromResult(Sanctions.FirstOrDefault(s => s.OwnerProfileId == ownerProfileId && s.Status == SanctionStatus.Active
                && s.ParkingLotId is null && (s.Level is SanctionLevel.TemporarySuspension or SanctionLevel.PermanentBan)));

        public Task AddLockAsync(Sanction sanction, AuditLog auditLog, CancellationToken cancellationToken)
        {
            if (AddLockFailure is not null) throw AddLockFailure;
            OnAddLock?.Invoke();
            AddLockCount++;
            Sanctions.Add(sanction);
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
