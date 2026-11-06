using AdminService.Application.Features.Owners;
using AdminService.Application.Features.Sanctions;
using AdminService.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using static AdminService.Test.SanctionFixtures;

namespace AdminService.Test;

// US-097: gỡ chế tài – chế tài cấp bãi ghi Revoked + audit; khóa chủ bãi đi qua luồng mở khóa US-096.
public class SanctionRevokeTests
{
    private static readonly DateTime NextWeek = NowUtc.AddDays(7);

    private static (RevokeSanctionUseCase UseCase, FakeOwnerDirectoryUserService Users) Create(InMemorySanctionStore store)
    {
        var users = new FakeOwnerDirectoryUserService(TsnOwner);
        var time = new FixedTime(Now);
        var unlock = new UnlockOwnerUseCase(users, store, time, NullLogger<UnlockOwnerUseCase>.Instance);
        return (new RevokeSanctionUseCase(store, unlock, store, time), users);
    }

    [Fact]
    public async Task Revoking_a_listing_suspension_records_who_when_why_and_queues_parking_and_notification_sync()
    {
        var sanction = LotSanction(7, LotBenThanh, SanctionLevel.TemporarySuspension, NextWeek);
        sanction.ParkingSyncStatus = SanctionSyncStatus.Synced;
        sanction.ParkingSyncedAtUtc = NowUtc.AddHours(-1);
        sanction.NotificationSyncStatus = SanctionSyncStatus.Synced;
        var store = new InMemorySanctionStore([sanction]);
        var (useCase, users) = Create(store);

        var detail = await useCase.ExecuteAsync(7, new RevokeSanctionRequest("  Chủ bãi đã khắc phục  "), Admin);

        Assert.Equal(SanctionStatus.Revoked, sanction.Status);
        Assert.Equal((NowUtc, Admin, "Chủ bãi đã khắc phục"), (sanction.RevokedAtUtc!.Value, sanction.RevokedByUserId!.Value, sanction.RevokeReason));
        Assert.Equal(SanctionSyncStatus.Pending, sanction.ParkingSyncStatus);        // ParkingService phải nhận lệnh gỡ
        Assert.Null(sanction.ParkingSyncedAtUtc);
        Assert.Equal(SanctionSyncStatus.Pending, sanction.NotificationSyncStatus);
        Assert.Equal(SanctionSyncStatus.NotRequired, sanction.UserServiceSyncStatus);
        var audit = Assert.Single(store.AuditLogs);
        Assert.Equal(("Sanction.Revoked", "Sanction", "7", "Chủ bãi đã khắc phục"), (audit.Action, audit.EntityName, audit.EntityId, audit.Reason));
        Assert.Equal(1, store.SaveCount);                                           // trạng thái + audit: 1 lần SaveChanges
        Assert.Empty(users.UnlockCalls);
        Assert.Equal("Revoked", detail.Sanction.Status);
        Assert.Equal("Sanction.Revoked", Assert.Single(detail.Timeline).Action);
    }

    [Fact]
    public async Task Revoking_a_warning_leaves_parking_sync_not_required()
    {
        var warning = LotSanction(8, LotBenThanh, SanctionLevel.Warning, NextWeek);
        warning.NotificationSyncStatus = SanctionSyncStatus.Synced;
        var (useCase, _) = Create(new InMemorySanctionStore([warning]));

        await useCase.ExecuteAsync(8, new RevokeSanctionRequest("Gỡ nhầm"), Admin);

        Assert.Equal(SanctionSyncStatus.NotRequired, warning.ParkingSyncStatus);
        Assert.Equal(SanctionSyncStatus.Pending, warning.NotificationSyncStatus);
    }

    [Fact]
    public async Task Revoking_an_owner_lock_goes_through_us096_unlock()
    {
        var ownerLock = OwnerLock(20, SanctionLevel.PermanentBan, null);
        var store = new InMemorySanctionStore([ownerLock]);
        var (useCase, users) = Create(store);

        var detail = await useCase.ExecuteAsync(20, new RevokeSanctionRequest("Khiếu nại được chấp nhận"), Admin);

        Assert.Equal((OwnerTsn, "Khiếu nại được chấp nhận"), Assert.Single(users.UnlockCalls));
        Assert.Equal(SanctionStatus.Revoked, ownerLock.Status);
        Assert.Equal("Khiếu nại được chấp nhận", ownerLock.RevokeReason);
        Assert.Equal(SanctionSyncStatus.Synced, ownerLock.UserServiceSyncStatus);
        Assert.Equal("Owner.Unlocked", Assert.Single(store.AuditLogs).Action);
        Assert.Contains(detail.Timeline, t => t.Action == "Owner.Unlocked");
    }

    [Fact]
    public async Task Revoked_expired_or_missing_sanctions_cannot_be_revoked()
    {
        var revoked = LotSanction(1, LotBenThanh, SanctionLevel.RankingPenalty, NextWeek, status: SanctionStatus.Revoked);
        var expired = LotSanction(2, LotBenThanh, SanctionLevel.TemporarySuspension, NowUtc.AddMinutes(-1));
        var (useCase, _) = Create(new InMemorySanctionStore([revoked, expired]));

        var a = await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(1, new RevokeSanctionRequest("x"), Admin));
        var b = await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(2, new RevokeSanctionRequest("x"), Admin));
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(99, new RevokeSanctionRequest("x"), Admin));

        Assert.Contains("đã được gỡ", a.Message);
        Assert.Contains("đã hết hạn", b.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Revoke_requires_a_reason(string? reason)
    {
        var sanction = LotSanction(7, LotBenThanh, SanctionLevel.RankingPenalty, NextWeek);
        var (useCase, _) = Create(new InMemorySanctionStore([sanction]));

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(7, new RevokeSanctionRequest(reason), Admin));

        Assert.Equal(SanctionStatus.Active, sanction.Status);
    }
}
