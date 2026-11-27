using System.Net;
using System.Text;
using System.Text.Json;
using AdminService.Application.Features.Owners;
using AdminService.Application.Features.Sanctions;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using AdminService.Infrastructure;
using AdminService.Infrastructure.Integrations;
using AdminService.Infrastructure.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using static AdminService.Test.SanctionFixtures;

namespace AdminService.Test;

// US-097 commit 3: thi hành chế tài ở service khác – đẩy ParkingService, thông báo chủ bãi, đồng bộ lại, job đóng chế tài tới hạn.
public class SanctionSyncTests
{
    private static readonly DateTime NextWeek = NowUtc.AddDays(7);

    private static IssueSanctionRequest Request(SanctionLevel level, int[]? lots = null, DateTime? endsAt = null, DateTime? correctionDue = null,
        decimal? penalty = null)
        => new(OwnerTsn, OwnerTsnUser, level, lots, "Từ chối 5 booking trong tuần", null, endsAt, correctionDue, penalty);

    private static (IssueSanctionUseCase UseCase, InMemorySanctionStore Store) Issue(FakeParkingSanctionClient parking,
        FakeNotificationClient notifications, InMemorySanctionStore? store = null)
    {
        store ??= new InMemorySanctionStore();
        var useCase = new IssueSanctionUseCase(new FakeOwnerDirectoryUserService(TsnOwner), store, store,
            new FakeLotDirectory((OwnerTsn, LotBenThanh), (OwnerTsn, LotTanSonNhat)), store, parking, notifications, new SanctionSettings(),
            new FixedTime(Now), NullLogger<IssueSanctionUseCase>.Instance);
        return (useCase, store);
    }

    // ===== Áp chế tài → đẩy sang service đích =====

    [Fact]
    public async Task Parking_service_down_keeps_the_sanction_and_marks_parking_failed()
    {
        var parking = new FakeParkingSanctionClient { Failure = new DependencyUnavailableException("ParkingService hiện không phản hồi.") };
        var notifications = new FakeNotificationClient();
        var (useCase, store) = Issue(parking, notifications);

        var result = await useCase.ExecuteAsync(Request(SanctionLevel.TemporarySuspension, [LotBenThanh, LotTanSonNhat], endsAt: NextWeek), Admin);

        Assert.Equal(2, store.Sanctions.Count);                                       // không mất chế tài
        Assert.All(result.Sanctions, s => Assert.Equal(("Failed", "Synced"), (s.ParkingSyncStatus, s.NotificationSyncStatus)));
        Assert.Equal(2, notifications.Sent.Count);
    }

    [Fact]
    public async Task Lot_suspension_is_sent_as_listing_suspension_and_owner_is_told_everything_in_ict()
    {
        var parking = new FakeParkingSanctionClient();
        var notifications = new FakeNotificationClient();
        var (useCase, store) = Issue(parking, notifications);

        await useCase.ExecuteAsync(Request(SanctionLevel.TemporarySuspension, [LotBenThanh], endsAt: NextWeek, penalty: 1_000_000m), Admin);

        var sanction = Assert.Single(store.Sanctions);
        Assert.Equal(new ParkingSanctionCommand(sanction.Id, OwnerTsn, LotBenThanh, "ListingSuspension", NowUtc, NextWeek),
            Assert.Single(parking.Applied));
        var sent = Assert.Single(notifications.Sent);
        Assert.Equal((OwnerTsnUser, "SANCTION_ISSUED"), (sent.UserId, sent.TemplateKey));
        Assert.Contains($"bãi #{LotBenThanh}", sent.Title);
        Assert.Contains("Lý do: Từ chối 5 booking trong tuần", sent.Body);
        Assert.Contains("tạm ngừng nhận đặt chỗ mới đến 15:00 13/11/2026", sent.Body);   // 08:00Z = 15:00 ICT
        Assert.Contains("Tiền phạt: 1.000.000 đ", sent.Body);
        Assert.Contains("khiếu nại đến 23:59 13/11/2026", sent.Body);                     // 5 ngày làm việc ICT
        using var data = JsonDocument.Parse(sent.DataJson);
        Assert.Equal(sanction.Id, data.RootElement.GetProperty("sanctionId").GetInt32());
    }

    [Fact]
    public async Task Owner_level_ranking_penalty_is_sent_without_a_lot()
    {
        var parking = new FakeParkingSanctionClient();
        var (useCase, _) = Issue(parking, new FakeNotificationClient());

        await useCase.ExecuteAsync(Request(SanctionLevel.RankingPenalty, endsAt: NowUtc.AddDays(10)), Admin);

        var command = Assert.Single(parking.Applied);
        Assert.Equal((null, "RankingPenalty"), (command.ParkingLotId, command.Kind));
    }

    [Fact]
    public async Task Notification_failure_does_not_affect_parking_sync()
    {
        var notifications = new FakeNotificationClient { Failure = new DependencyUnavailableException("down") };
        var (useCase, store) = Issue(new FakeParkingSanctionClient(), notifications);

        await useCase.ExecuteAsync(Request(SanctionLevel.RankingPenalty, [LotBenThanh], endsAt: NextWeek), Admin);

        var sanction = Assert.Single(store.Sanctions);
        Assert.Equal((SanctionSyncStatus.Synced, SanctionSyncStatus.Failed), (sanction.ParkingSyncStatus, sanction.NotificationSyncStatus));
        Assert.Null(sanction.NotificationSentAtUtc);
    }

    [Fact]
    public async Task Rejected_admin_token_is_recorded_as_failed_not_thrown()
    {
        var parking = new FakeParkingSanctionClient { Failure = new ForbiddenException("Không có quyền") };
        var (useCase, store) = Issue(parking, new FakeNotificationClient());

        await useCase.ExecuteAsync(Request(SanctionLevel.RankingPenalty, [LotBenThanh], endsAt: NextWeek), Admin);

        Assert.Equal(SanctionSyncStatus.Failed, Assert.Single(store.Sanctions).ParkingSyncStatus);
    }

    // ===== Nội dung thông báo =====

    [Fact]
    public void Notifications_cover_warning_ban_revoke_and_expiry()
    {
        var warning = Sanction.Issue(OwnerTsn, null, SanctionLevel.Warning, "Phản hồi chậm", Admin, NowUtc, null, NextWeek,
            new SanctionExtras(OwnerTsnUser, null, null, NowUtc.AddDays(7))).WithId(1);
        var warningText = SanctionNotifications.Compose(warning, OwnerTsnUser);
        Assert.Contains("toàn bộ bãi của bạn", warningText.Title);
        Assert.Contains("Hạn khắc phục: 15:00 13/11/2026.", warningText.Body);
        Assert.DoesNotContain("Tiền phạt", warningText.Body);

        var ban = OwnerLock(2, SanctionLevel.PermanentBan, null);
        Assert.Contains("khóa vĩnh viễn", SanctionNotifications.Compose(ban, OwnerTsnUser).Body);

        var expiredLock = OwnerLock(3, SanctionLevel.TemporarySuspension, NowUtc.AddHours(-1), SanctionStatus.Expired);
        var expired = SanctionNotifications.Compose(expiredLock, OwnerTsnUser);
        Assert.Equal("SANCTION_EXPIRED", expired.TemplateKey);
        Assert.Contains("hết hạn lúc 14:00 06/11/2026", expired.Body);
        Assert.Contains("mở khóa sau khi quản trị viên xác nhận", expired.Body);

        var revoked = LotSanction(4, LotBenThanh, SanctionLevel.RankingPenalty, NextWeek, status: SanctionStatus.Revoked);
        revoked.RevokeReason = "Khiếu nại hợp lệ";
        var revokedText = SanctionNotifications.Compose(revoked, OwnerTsnUser);
        Assert.Equal(("SANCTION_REVOKED", "Đã gỡ chế tài #4"), (revokedText.TemplateKey, revokedText.Title));
        Assert.Contains("Lý do gỡ: Khiếu nại hợp lệ", revokedText.Body);
    }

    // ===== Đồng bộ lại (Admin) =====

    private sealed record RetryHarness(RetrySanctionSyncUseCase UseCase, InMemorySanctionStore Store, FakeOwnerDirectoryUserService Users,
        FakeParkingSanctionClient Parking, FakeNotificationClient Notifications);

    private static RetryHarness Retry(params Sanction[] sanctions)
    {
        var store = new InMemorySanctionStore(sanctions);
        var users = new FakeOwnerDirectoryUserService(TsnOwner);
        var parking = new FakeParkingSanctionClient();
        var notifications = new FakeNotificationClient();
        return new RetryHarness(new RetrySanctionSyncUseCase(store, users, parking, notifications, store, new FixedTime(Now),
            NullLogger<RetrySanctionSyncUseCase>.Instance), store, users, parking, notifications);
    }

    private static Sanction WithRecipient(Sanction sanction)
    {
        sanction.OwnerUserId = OwnerTsnUser;
        return sanction;
    }

    [Fact]
    public async Task Retry_resends_only_failed_targets_and_audits_the_attempt()
    {
        var sanction = WithRecipient(LotSanction(7, LotBenThanh, SanctionLevel.RankingPenalty, NextWeek));
        sanction.ParkingSyncStatus = SanctionSyncStatus.Failed;
        sanction.NotificationSyncStatus = SanctionSyncStatus.Synced;
        var h = Retry(sanction);

        var detail = await h.UseCase.ExecuteAsync(7, Admin);

        Assert.Equal(7, Assert.Single(h.Parking.Applied).SanctionId);
        Assert.Empty(h.Notifications.Sent);                                           // đã gửi rồi, không gửi lại
        Assert.Equal("Synced", detail.Sanction.ParkingSyncStatus);
        var audit = Assert.Single(h.Store.AuditLogs);
        Assert.Equal(("Sanction.SyncRetried", Admin), (audit.Action, audit.UserId!.Value));
        Assert.Contains("\"parking\":\"Failed\"", audit.OldValuesJson);
        Assert.Contains("\"parking\":\"Synced\"", audit.NewValuesJson);
        Assert.Equal(1, h.Store.SaveCount);                                           // kết quả + audit: 1 lần SaveChanges
    }

    [Fact]
    public async Task Retry_of_seeded_pending_lot_sanction_pushes_it_to_parking()
    {
        var seeded = LotSanction(3, LotBenThanh, SanctionLevel.TemporarySuspension, NextWeek);
        seeded.ParkingSyncStatus = SanctionSyncStatus.Pending;                        // dòng seed LotBenThanh
        var h = Retry(seeded);

        await h.UseCase.ExecuteAsync(3, Admin);

        Assert.Equal("ListingSuspension", Assert.Single(h.Parking.Applied).Kind);
        Assert.Equal(SanctionSyncStatus.Synced, seeded.ParkingSyncStatus);
    }

    [Fact]
    public async Task Retry_of_revoked_lot_sanction_lifts_it()
    {
        var revoked = LotSanction(8, LotBenThanh, SanctionLevel.RankingPenalty, NextWeek, status: SanctionStatus.Revoked);
        revoked.ParkingSyncStatus = SanctionSyncStatus.Failed;
        var h = Retry(revoked);

        await h.UseCase.ExecuteAsync(8, Admin);

        Assert.Equal([8], h.Parking.Lifted);
    }

    [Fact]
    public async Task Retry_of_expired_owner_lock_unlocks_in_user_service_with_admin_token()
    {
        var expired = WithRecipient(OwnerLock(20, SanctionLevel.TemporarySuspension, NowUtc.AddHours(-2), SanctionStatus.Expired));
        expired.UserServiceSyncStatus = SanctionSyncStatus.Pending;                    // job không có token
        expired.NotificationSyncStatus = SanctionSyncStatus.Synced;
        var h = Retry(expired);

        await h.UseCase.ExecuteAsync(20, Admin);

        Assert.Equal((OwnerTsn, "Chế tài #20 hết hạn."), Assert.Single(h.Users.UnlockCalls));
        Assert.Empty(h.Users.LockCalls);
        Assert.Equal(SanctionSyncStatus.Synced, expired.UserServiceSyncStatus);
    }

    [Fact]
    public async Task Retry_of_active_owner_lock_with_failed_user_service_locks_again()
    {
        var active = OwnerLock(21, SanctionLevel.PermanentBan, null);
        active.UserServiceSyncStatus = SanctionSyncStatus.Failed;
        var h = Retry(active);

        await h.UseCase.ExecuteAsync(21, Admin);

        Assert.Single(h.Users.LockCalls);
        Assert.Equal(SanctionSyncStatus.Synced, active.UserServiceSyncStatus);
    }

    [Fact]
    public async Task Retry_closes_an_overdue_active_sanction_first()
    {
        var overdue = WithRecipient(LotSanction(9, LotBenThanh, SanctionLevel.RankingPenalty, NowUtc.AddHours(-1)));
        overdue.ParkingSyncStatus = SanctionSyncStatus.Failed;
        overdue.NotificationSyncStatus = SanctionSyncStatus.Synced;
        var h = Retry(overdue);

        var detail = await h.UseCase.ExecuteAsync(9, Admin);

        Assert.Equal(SanctionStatus.Expired, overdue.Status);
        Assert.Empty(h.Parking.Applied);                                              // hết hạn: không áp nữa
        Assert.Equal(SanctionSyncStatus.NotRequired, overdue.ParkingSyncStatus);
        Assert.Equal("SANCTION_EXPIRED", Assert.Single(h.Notifications.Sent).TemplateKey);
        Assert.Equal(["Sanction.Expired", "Sanction.SyncRetried"], h.Store.AuditLogs.Select(a => a.Action));
        Assert.Equal("Expired", detail.Sanction.Status);
    }

    [Fact]
    public async Task Retry_without_anything_to_sync_is_409_and_other_errors_map_to_400_404()
    {
        var synced = LotSanction(10, LotBenThanh, SanctionLevel.RankingPenalty, NextWeek);
        synced.ParkingSyncStatus = SanctionSyncStatus.Synced;
        var h = Retry(synced);

        await Assert.ThrowsAsync<ConflictException>(() => h.UseCase.ExecuteAsync(10, Admin));
        await Assert.ThrowsAsync<NotFoundException>(() => h.UseCase.ExecuteAsync(99, Admin));
        await Assert.ThrowsAsync<ValidationException>(() => h.UseCase.ExecuteAsync(0, Admin));
        Assert.Empty(h.Store.AuditLogs);
    }

    [Fact]
    public async Task Retry_racing_the_job_is_409()
    {
        var overdue = LotSanction(11, LotBenThanh, SanctionLevel.RankingPenalty, NowUtc.AddHours(-1));
        var h = Retry(overdue);
        h.Store.ExpiredElsewhere.Add(11);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.UseCase.ExecuteAsync(11, Admin));
        Assert.Contains("vừa được hệ thống đóng", ex.Message);
    }

    // ===== Job đóng chế tài tới hạn =====

    private static (ExpireSanctionsUseCase UseCase, FakeParkingSanctionClient Parking, FakeNotificationClient Notifications) Expire(
        InMemorySanctionStore store, FakeNotificationClient? notifications = null)
    {
        var parking = new FakeParkingSanctionClient();
        notifications ??= new FakeNotificationClient();
        return (new ExpireSanctionsUseCase(store, parking, notifications, new FixedTime(Now), NullLogger<ExpireSanctionsUseCase>.Instance),
            parking, notifications);
    }

    [Fact]
    public async Task Job_expires_due_sanctions_notifies_owner_and_leaves_owner_unlock_pending()
    {
        var dueRanking = WithRecipient(LotSanction(1, LotBenThanh, SanctionLevel.RankingPenalty, NowUtc.AddMinutes(-5)));
        dueRanking.ParkingSyncStatus = SanctionSyncStatus.Synced;
        dueRanking.NotificationSyncStatus = SanctionSyncStatus.Synced;
        var dueLock = WithRecipient(OwnerLock(2, SanctionLevel.TemporarySuspension, NowUtc));          // EndsAt = now → tới hạn
        dueLock.UserServiceSyncStatus = SanctionSyncStatus.Synced;
        dueLock.NotificationSyncStatus = SanctionSyncStatus.Synced;
        var notDue = LotSanction(3, LotTanSonNhat, SanctionLevel.RankingPenalty, NextWeek);
        var revoked = LotSanction(4, LotBenThanh, SanctionLevel.Warning, NowUtc.AddDays(-1), status: SanctionStatus.Revoked);
        var permanent = OwnerLock(5, SanctionLevel.PermanentBan, null, ownerProfileId: OwnerVincom);
        var store = new InMemorySanctionStore([dueRanking, dueLock, notDue, revoked, permanent]);
        var (useCase, parking, notifications) = Expire(store);

        var count = await useCase.ExecuteAsync(batchSize: 100);

        Assert.Equal(2, count);
        Assert.Equal([SanctionStatus.Expired, SanctionStatus.Expired, SanctionStatus.Active, SanctionStatus.Revoked, SanctionStatus.Active],
            store.Sanctions.Select(s => s.Status));
        Assert.Empty(parking.Applied);
        Assert.Empty(parking.Lifted);                                                 // ParkingService tự hết hiệu lực
        Assert.Equal(SanctionSyncStatus.Synced, dueRanking.ParkingSyncStatus);
        Assert.Equal(SanctionSyncStatus.Pending, dueLock.UserServiceSyncStatus);      // chờ Admin đồng bộ lại (không có token)
        Assert.Null(dueLock.UserServiceSyncedAtUtc);
        Assert.Equal(["SANCTION_EXPIRED", "SANCTION_EXPIRED"], notifications.Sent.Select(n => n.TemplateKey));
        Assert.All([dueRanking, dueLock], s => Assert.Equal(SanctionSyncStatus.Synced, s.NotificationSyncStatus));
        Assert.All(store.AuditLogs, a => Assert.Equal(("Sanction.Expired", (int?)null), (a.Action, a.UserId)));
        Assert.Equal(["1", "2"], store.AuditLogs.Select(a => a.EntityId));
    }

    [Fact]
    public async Task Job_skips_sanctions_another_instance_already_closed()
    {
        var due = WithRecipient(LotSanction(1, LotBenThanh, SanctionLevel.RankingPenalty, NowUtc.AddMinutes(-5)));
        var store = new InMemorySanctionStore([due]);
        store.ExpiredElsewhere.Add(1);
        var (useCase, _, notifications) = Expire(store);

        Assert.Equal(0, await useCase.ExecuteAsync(batchSize: 100));
        Assert.Empty(notifications.Sent);
        Assert.Empty(store.AuditLogs);
    }

    [Fact]
    public async Task Job_respects_batch_size_and_keeps_expiry_when_notification_fails()
    {
        var store = new InMemorySanctionStore(Enumerable.Range(1, 3).Select(i =>
        {
            var warning = WithRecipient(LotSanction(i, LotBenThanh, SanctionLevel.Warning, NowUtc.AddMinutes(-i)));
            warning.NotificationSyncStatus = SanctionSyncStatus.Synced;              // đã báo lúc áp → hết hạn phải báo tiếp
            return warning;
        }));
        var (useCase, _, _) = Expire(store, new FakeNotificationClient { Failure = new DependencyUnavailableException("down") });

        Assert.Equal(2, await useCase.ExecuteAsync(batchSize: 2));
        Assert.Equal([3, 2], store.Sanctions.Where(s => s.Status == SanctionStatus.Expired).Select(s => s.Id).OrderDescending());   // hạn sớm nhất trước
        Assert.All(store.Sanctions.Where(s => s.Status == SanctionStatus.Expired),
            s => Assert.Equal(SanctionSyncStatus.Failed, s.NotificationSyncStatus));
        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(batchSize: 0));
    }

    [Fact]
    public void Us096_lock_without_recipient_expires_without_notification()
    {
        var us096 = OwnerLock(1, SanctionLevel.TemporarySuspension, NowUtc);

        us096.ExpireOnSchedule();

        Assert.Equal((SanctionSyncStatus.Pending, SanctionSyncStatus.NotRequired), (us096.UserServiceSyncStatus, us096.NotificationSyncStatus));
    }

    [Fact]
    public async Task Job_run_drains_full_batches_and_swallows_errors()
    {
        var fake = new ScriptedExpireUseCase(2, 2, 1);
        var job = new SanctionExpiryJob(Scopes(fake), new SanctionExpiryJobSettings(BatchSize: 2), NullLogger<SanctionExpiryJob>.Instance);

        await job.RunOnceAsync(CancellationToken.None);
        Assert.Equal(3, fake.Calls);                                                  // 2 + 2 + 1 → lô cuối chưa đầy thì dừng

        var failing = new ScriptedExpireUseCase { Failure = new InvalidOperationException("DB down") };
        await new SanctionExpiryJob(Scopes(failing), new SanctionExpiryJobSettings(), NullLogger<SanctionExpiryJob>.Instance)
            .RunOnceAsync(CancellationToken.None);                                      // không ném lỗi
        Assert.Equal(1, failing.Calls);
    }

    private static IServiceScopeFactory Scopes(IExpireSanctionsUseCase useCase)
        => new ServiceCollection().AddSingleton(useCase).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    private sealed class ScriptedExpireUseCase(params int[] results) : IExpireSanctionsUseCase
    {
        public int Calls { get; private set; }
        public Exception? Failure { get; init; }

        public Task<int> ExecuteAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Failure is not null) throw Failure;
            return Task.FromResult(results[Calls - 1]);
        }
    }

    // ===== HTTP client =====

    [Fact]
    public async Task Parking_client_puts_and_deletes_sanction_state()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        var client = new ParkingSanctionClient(Http(handler), NullLogger<ParkingSanctionClient>.Instance);

        await client.ApplyAsync(new ParkingSanctionCommand(12, OwnerTsn, LotBenThanh, "ListingSuspension", NowUtc, NextWeek), default);
        await client.LiftAsync(12, default);

        Assert.Equal([("PUT", "/api/v1/parking-lots/sanction-states/12"), ("DELETE", "/api/v1/parking-lots/sanction-states/12")],
            handler.Requests.Select(r => (r.Method, r.Path)));
        using var body = JsonDocument.Parse(handler.Requests[0].Body!);
        Assert.Equal(OwnerTsn, body.RootElement.GetProperty("ownerProfileId").GetInt32());
        Assert.Equal(LotBenThanh, body.RootElement.GetProperty("parkingLotId").GetInt32());
        Assert.Equal("ListingSuspension", body.RootElement.GetProperty("kind").GetString());
        Assert.Equal(NextWeek, body.RootElement.GetProperty("endsAtUtc").GetDateTime().ToUniversalTime());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.Forbidden, typeof(ForbiddenException))]
    [InlineData(HttpStatusCode.Conflict, typeof(DependencyUnavailableException))]
    [InlineData(HttpStatusCode.BadGateway, typeof(DependencyUnavailableException))]
    public async Task Parking_client_maps_errors(HttpStatusCode status, Type expected)
    {
        var client = new ParkingSanctionClient(Http(new RecordingHandler(_ => new HttpResponseMessage(status))),
            NullLogger<ParkingSanctionClient>.Instance);

        Assert.IsType(expected, await Record.ExceptionAsync(() => client.LiftAsync(1, default)));
    }

    [Fact]
    public async Task Parking_client_network_error_is_dependency_unavailable()
    {
        var client = new ParkingSanctionClient(Http(new RecordingHandler(_ => throw new HttpRequestException("refused"))),
            NullLogger<ParkingSanctionClient>.Instance);

        await Assert.ThrowsAsync<DependencyUnavailableException>(() => client.LiftAsync(1, default));
    }

    [Theory]
    [InlineData("Sent", false)]
    [InlineData("Pending", false)]          // NotificationService tự gửi lại
    [InlineData("Failed", true)]
    public async Task Notification_client_posts_in_app_and_treats_failed_status_as_error(string status, bool fails)
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{\"notificationId\":5,\"status\":\"{status}\",\"message\":\"x\"}}", Encoding.UTF8, "application/json"),
        });
        var client = new NotificationClient(Http(handler), NullLogger<NotificationClient>.Instance);

        var error = await Record.ExceptionAsync(() => client.SendAsync(new OwnerNotification(OwnerTsnUser, "SANCTION_ISSUED", "T", "B", "{}"), default));

        Assert.Equal(fails, error is DependencyUnavailableException);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(("POST", "/api/v1/notifications/send"), (request.Method, request.Path));
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal(("InApp", OwnerTsnUser, "SANCTION_ISSUED"), (body.RootElement.GetProperty("channel").GetString(),
            body.RootElement.GetProperty("userId").GetInt32(), body.RootElement.GetProperty("templateKey").GetString()));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("false", false)]
    public void Infrastructure_registers_sync_clients_and_optional_expiry_job(string? enabled, bool expectJob)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAdminInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ServiceDb"] = "Host=localhost;Database=pm_admin_unit_test",
            ["Services:GatewayBaseUrl"] = "http://localhost:5000",
            ["Sanctions:ExpiryJob:Enabled"] = enabled,
            ["Sanctions:ExpiryJob:IntervalSeconds"] = "5",           // ngoài khoảng 30–3600 → mặc định
        }).Build());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.IsType<ParkingSanctionClient>(scope.ServiceProvider.GetRequiredService<IParkingSanctionClient>());
        Assert.IsType<NotificationClient>(scope.ServiceProvider.GetRequiredService<INotificationClient>());
        Assert.Equal(300, provider.GetRequiredService<SanctionExpiryJobSettings>().IntervalSeconds);
        Assert.Equal(expectJob, provider.GetServices<IHostedService>().OfType<SanctionExpiryJob>().Any());
    }

    private static HttpClient Http(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("http://gateway.test/") };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(string Method, string Path, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method.Method, request.RequestUri!.AbsolutePath,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return respond(request);
        }
    }
}
