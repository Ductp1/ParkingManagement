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

    private static (RevokeSanctionUseCase UseCase, FakeOwnerDirectoryUserService Users) Create(InMemorySanctionStore store,
        FakeParkingSanctionClient? parking = null, FakeNotificationClient? notifications = null)
    {
        var users = new FakeOwnerDirectoryUserService(TsnOwner);
        var time = new FixedTime(Now);
        var unlock = new UnlockOwnerUseCase(users, store, time, NullLogger<UnlockOwnerUseCase>.Instance);
        return (new RevokeSanctionUseCase(store, unlock, store, parking ?? new FakeParkingSanctionClient(),
            notifications ?? new FakeNotificationClient(), time, NullLogger<RevokeSanctionUseCase>.Instance), users);
    }

    [Fact]
    public async Task Revoking_a_listing_suspension_records_who_when_why_then_lifts_it_in_parking_and_notifies()
    {
        var sanction = LotSanction(7, LotBenThanh, SanctionLevel.TemporarySuspension, NextWeek);
        sanction.ParkingSyncStatus = SanctionSyncStatus.Synced;
        sanction.ParkingSyncedAtUtc = NowUtc.AddHours(-1);
        sanction.NotificationSyncStatus = SanctionSyncStatus.Synced;
        sanction.OwnerUserId = OwnerTsnUser;
        var store = new InMemorySanctionStore([sanction]);
        var parking = new FakeParkingSanctionClient();
        var notifications = new FakeNotificationClient();
        var (useCase, users) = Create(store, parking, notifications);

        var detail = await useCase.ExecuteAsync(7, new RevokeSanctionRequest("  Chủ bãi đã khắc phục  "), Admin);

        Assert.Equal(SanctionStatus.Revoked, sanction.Status);
        Assert.Equal((NowUtc, Admin, "Chủ bãi đã khắc phục"), (sanction.RevokedAtUtc!.Value, sanction.RevokedByUserId!.Value, sanction.RevokeReason));
        Assert.Equal([7], parking.Lifted);                                          // ParkingService nhận lệnh gỡ
        Assert.Equal((SanctionSyncStatus.Synced, (DateTime?)NowUtc), (sanction.ParkingSyncStatus, sanction.ParkingSyncedAtUtc));
        Assert.Equal(SanctionSyncStatus.Synced, sanction.NotificationSyncStatus);
        var sent = Assert.Single(notifications.Sent);
        Assert.Equal("SANCTION_REVOKED", sent.TemplateKey);
        Assert.Contains("Chủ bãi đã khắc phục", sent.Body);
        Assert.Equal(SanctionSyncStatus.NotRequired, sanction.UserServiceSyncStatus);
        var audit = Assert.Single(store.AuditLogs);
        Assert.Equal(("Sanction.Revoked", "Sanction", "7", "Chủ bãi đã khắc phục"), (audit.Action, audit.EntityName, audit.EntityId, audit.Reason));
        Assert.Equal(2, store.SaveCount);                                           // trạng thái + audit (1 SaveChanges), rồi kết quả đồng bộ
        Assert.Empty(users.UnlockCalls);
        Assert.Equal("Revoked", detail.Sanction.Status);
        Assert.Equal("Sanction.Revoked", Assert.Single(detail.Timeline).Action);
    }

    [Fact]
    public async Task Revoking_a_warning_leaves_parking_sync_not_required()
    {
        var warning = LotSanction(8, LotBenThanh, SanctionLevel.Warning, NextWeek);
        warning.NotificationSyncStatus = SanctionSyncStatus.Synced;
        warning.OwnerUserId = OwnerTsnUser;
        var (useCase, _) = Create(new InMemorySanctionStore([warning]));

        await useCase.ExecuteAsync(8, new RevokeSanctionRequest("Gỡ nhầm"), Admin);

        Assert.Equal(SanctionSyncStatus.NotRequired, warning.ParkingSyncStatus);
        Assert.Equal(SanctionSyncStatus.Synced, warning.NotificationSyncStatus);
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
