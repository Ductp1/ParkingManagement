using System.Text.Json;
using AdminService.Application.Features.Sanctions;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using static AdminService.Test.SanctionFixtures;

namespace AdminService.Test;

// US-097: áp chế tài 4 cấp (Điều 7 hợp đồng mẫu) – quy tắc từng cấp, nhiều bãi / 1 request, chống trùng, tái dùng khóa US-096.
public class SanctionIssueTests
{
    private static readonly DateTime NextWeek = NowUtc.AddDays(7);

    private static (IssueSanctionUseCase UseCase, InMemorySanctionStore Store, FakeOwnerDirectoryUserService Users, FakeLotDirectory Lots)
        Create(InMemorySanctionStore? store = null, FakeOwnerDirectoryUserService? users = null, FakeLotDirectory? lots = null,
            FakeParkingSanctionClient? parking = null, FakeNotificationClient? notifications = null)
    {
        store ??= new InMemorySanctionStore();
        users ??= new FakeOwnerDirectoryUserService(TsnOwner, VincomOwner);
        lots ??= new FakeLotDirectory((OwnerTsn, LotBenThanh), (OwnerTsn, LotTanSonNhat), (OwnerVincom, LotVincom));
        var useCase = new IssueSanctionUseCase(users, store, store, lots, store, parking ?? new FakeParkingSanctionClient(),
            notifications ?? new FakeNotificationClient(), new SanctionSettings(), new FixedTime(Now), NullLogger<IssueSanctionUseCase>.Instance);
        return (useCase, store, users, lots);
    }

    private static IssueSanctionRequest Request(SanctionLevel? level, int[]? lots = null, DateTime? endsAt = null, DateTime? correctionDue = null,
        decimal? penalty = null, int? riskFlagId = null, string? reason = "  Từ chối 5 booking trong tuần  ", string? evidence = null)
        => new(OwnerTsn, OwnerTsnUser, level, lots, reason, evidence, endsAt, correctionDue, penalty, riskFlagId);

    // ===== Cấp 1 – Cảnh cáo =====

    [Fact]
    public async Task Warning_on_two_lots_creates_one_sanction_per_lot_with_audit_in_one_transaction()
    {
        var notifications = new FakeNotificationClient();
        var parking = new FakeParkingSanctionClient();
        var (useCase, store, users, _) = Create(parking: parking, notifications: notifications);

        var result = await useCase.ExecuteAsync(Request(SanctionLevel.Warning, [LotBenThanh, LotTanSonNhat, LotBenThanh], correctionDue: NextWeek,
            penalty: 500_000m, evidence: "{\"bookings\":[\"BK-1\",\"BK-2\"]}"), Admin);

        Assert.Equal(2, result.Sanctions.Count);                         // bãi trùng trong request bị bỏ
        Assert.Equal(1, store.TransactionCount);
        Assert.Equal([LotBenThanh, LotTanSonNhat], store.Sanctions.Select(s => s.ParkingLotId!.Value));
        Assert.All(store.Sanctions, s =>
        {
            Assert.Equal(SanctionLevel.Warning, s.Level);
            Assert.Equal("Từ chối 5 booking trong tuần", s.Reason);
            Assert.Equal(NowUtc, s.StartsAtUtc);
            Assert.Equal(NextWeek, s.CorrectionDueAtUtc);
            Assert.Equal(NextWeek, s.EndsAtUtc);                          // cảnh cáo hết hiệu lực ở hạn khắc phục
            Assert.Equal(OwnerTsnUser, s.OwnerUserId);
            Assert.Equal(500_000m, s.PenaltyAmount);
            Assert.Equal("{\"bookings\":[\"BK-1\",\"BK-2\"]}", s.EvidenceJson);
            Assert.Equal(SanctionSyncStatus.NotRequired, s.UserServiceSyncStatus);
            Assert.Equal(SanctionSyncStatus.NotRequired, s.ParkingSyncStatus);            // cảnh cáo chỉ cần thông báo
            Assert.Equal((SanctionSyncStatus.Synced, (DateTime?)NowUtc), (s.NotificationSyncStatus, s.NotificationSentAtUtc));
            Assert.Equal(new DateTime(2026, 11, 13, 16, 59, 59, DateTimeKind.Utc), s.AppealDeadlineUtc);   // T6 → T6 tuần sau 23:59:59 ICT
        });
        Assert.Empty(parking.Applied);
        Assert.Equal(2, notifications.Sent.Count);
        Assert.All(notifications.Sent, n => Assert.Equal((OwnerTsnUser, "SANCTION_ISSUED"), (n.UserId, n.TemplateKey)));
        Assert.Equal(2, store.AuditLogs.Count);
        Assert.All(store.AuditLogs, a => Assert.Equal(("Sanction.Issued", "Sanction"), (a.Action, a.EntityName)));
        Assert.Equal(store.Sanctions.Select(s => s.Id.ToString()), store.AuditLogs.Select(a => a.EntityId));
        Assert.Empty(users.LockCalls);                                    // cảnh cáo không khóa tài khoản
    }

    [Fact]
    public async Task Warning_without_lots_applies_to_whole_owner()
    {
        var (useCase, store, _, lots) = Create();

        var result = await useCase.ExecuteAsync(Request(SanctionLevel.Warning, correctionDue: NextWeek), Admin);

        Assert.Null(Assert.Single(result.Sanctions).ParkingLotId);
        Assert.Equal(0, lots.Calls);                                      // không có bãi → không cần hỏi ParkingService
        Assert.Equal("Active", result.Sanctions[0].Status);
        Assert.Single(store.Sanctions);
    }

    [Theory]
    [InlineData(null, "bắt buộc có hạn khắc phục")]
    [InlineData(-1, "phải sau thời điểm hiện tại")]
    public async Task Warning_requires_future_correction_due(int? offsetDays, string message)
    {
        var (useCase, store, _, _) = Create();
        var due = offsetDays is { } days ? NowUtc.AddDays(days) : (DateTime?)null;

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(Request(SanctionLevel.Warning, correctionDue: due), Admin));

        Assert.Contains(message, ex.Message);
        Assert.Empty(store.Sanctions);
    }

    [Fact]
    public async Task Warning_rejects_ends_at()
    {
        var (useCase, _, _, _) = Create();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(Request(SanctionLevel.Warning, endsAt: NextWeek, correctionDue: NextWeek), Admin));

        Assert.Contains("correctionDueAtUtc", ex.Message);
    }

    // ===== Cấp 2 – Hạ thứ hạng =====

    [Fact]
    public async Task Ranking_penalty_on_lots_is_pushed_to_parking_service_after_saving()
    {
        var parking = new FakeParkingSanctionClient();
        var (useCase, store, _, _) = Create(parking: parking);

        await useCase.ExecuteAsync(Request(SanctionLevel.RankingPenalty, [LotBenThanh], endsAt: NowUtc.AddDays(30)), Admin);

        var sanction = Assert.Single(store.Sanctions);
        Assert.Equal(new ParkingSanctionCommand(sanction.Id, OwnerTsn, LotBenThanh, "RankingPenalty", NowUtc, NowUtc.AddDays(30)),
            Assert.Single(parking.Applied));
        Assert.Equal((SanctionSyncStatus.Synced, (DateTime?)NowUtc), (sanction.ParkingSyncStatus, sanction.ParkingSyncedAtUtc));
        Assert.Equal(SanctionSyncStatus.Synced, sanction.NotificationSyncStatus);
        Assert.Equal((1, 1), (store.TransactionCount, store.SaveCount));           // chế tài + audit, rồi kết quả đồng bộ
        Assert.Equal(SanctionSyncStatus.NotRequired, sanction.UserServiceSyncStatus);
        Assert.Null(sanction.CorrectionDueAtUtc);
    }

    [Theory]
    [InlineData(null, "bắt buộc có thời điểm kết thúc")]
    [InlineData(0, "phải sau thời điểm hiện tại")]
    [InlineData(31, "tối đa 30 ngày")]
    public async Task Ranking_penalty_needs_end_within_30_days(int? offsetDays, string message)
    {
        var (useCase, _, _, _) = Create();
        var endsAt = offsetDays is { } days ? NowUtc.AddDays(days) : (DateTime?)null;

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(Request(SanctionLevel.RankingPenalty, [LotBenThanh], endsAt: endsAt), Admin));

        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public async Task Ranking_penalty_for_whole_owner_is_unique_while_in_effect()
    {
        var existing = new Sanction
        {
            OwnerProfileId = OwnerTsn, Level = SanctionLevel.RankingPenalty, Reason = "x", StartsAtUtc = NowUtc.AddDays(-1),
            EndsAtUtc = NowUtc.AddDays(5), IssuedByUserId = Admin,
        }.WithId(9);
        var (useCase, store, _, _) = Create(new InMemorySanctionStore([existing]));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(Request(SanctionLevel.RankingPenalty, endsAt: NextWeek), Admin));

        Assert.Contains("#9", ex.Message);
        Assert.Single(store.Sanctions);
    }

    // ===== Cấp 3 – Tạm ngừng niêm yết (bãi) =====

    [Fact]
    public async Task Listing_suspension_conflict_on_any_lot_rejects_whole_request_and_lists_the_lots()
    {
        var store = new InMemorySanctionStore([LotSanction(7, LotTanSonNhat, SanctionLevel.TemporarySuspension, NextWeek)]);
        var (useCase, _, _, _) = Create(store);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(Request(SanctionLevel.TemporarySuspension, [LotBenThanh, LotTanSonNhat], endsAt: NextWeek), Admin));

        Assert.Contains("Bãi 5", ex.Message);
        Assert.Contains("chế tài #7", ex.Message);
        Assert.Single(store.Sanctions);                                   // không bãi nào bị áp
        Assert.Empty(store.AuditLogs);
    }

    [Fact]
    public async Task Same_lot_may_have_a_ranking_penalty_and_a_listing_suspension()
    {
        var store = new InMemorySanctionStore([LotSanction(7, LotBenThanh, SanctionLevel.RankingPenalty, NextWeek)]);
        var (useCase, _, _, _) = Create(store);

        await useCase.ExecuteAsync(Request(SanctionLevel.TemporarySuspension, [LotBenThanh], endsAt: NextWeek), Admin);

        Assert.Equal(2, store.Sanctions.Count(s => s.Status == SanctionStatus.Active));
    }

    [Fact]
    public async Task Stale_active_lot_sanction_past_its_end_is_expired_to_make_room()
    {
        var stale = LotSanction(7, LotBenThanh, SanctionLevel.TemporarySuspension, NowUtc.AddMinutes(-1));
        var (useCase, store, _, _) = Create(new InMemorySanctionStore([stale]));

        await useCase.ExecuteAsync(Request(SanctionLevel.TemporarySuspension, [LotBenThanh], endsAt: NextWeek), Admin);

        Assert.Equal(SanctionStatus.Expired, stale.Status);
        Assert.Equal(2, store.Sanctions.Count);
    }

    [Fact]
    public async Task Lots_of_another_owner_are_rejected()
    {
        var (useCase, store, _, _) = Create();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(Request(SanctionLevel.TemporarySuspension, [LotBenThanh, LotVincom], endsAt: NextWeek), Admin));

        Assert.Contains($"Bãi {LotVincom} không thuộc chủ bãi {OwnerTsn}", ex.Message);
        Assert.Empty(store.Sanctions);
    }

    [Fact]
    public async Task Parking_service_unavailable_while_verifying_lots_saves_nothing()
    {
        var lots = new FakeLotDirectory { Failure = new DependencyUnavailableException("ParkingService hiện không phản hồi.") };
        var (useCase, store, _, _) = Create(lots: lots);

        await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            useCase.ExecuteAsync(Request(SanctionLevel.RankingPenalty, [LotBenThanh], endsAt: NextWeek), Admin));

        Assert.Empty(store.Sanctions);
    }

    [Fact]
    public async Task Concurrent_insert_blocked_by_unique_index_returns_conflict()
    {
        var store = new InMemorySanctionStore { FailNextInsertWithUniqueViolation = true };
        var (useCase, _, _, _) = Create(store);

        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(Request(SanctionLevel.RankingPenalty, [LotBenThanh], endsAt: NextWeek), Admin));
    }

    // ===== Cấp 3 toàn chủ bãi / cấp 4 – tái dùng khóa chủ bãi US-096 =====

    [Fact]
    public async Task Temporary_suspension_without_lots_is_a_temporary_account_lock_through_us096_core()
    {
        var (useCase, store, users, lots) = Create();

        var result = await useCase.ExecuteAsync(Request(SanctionLevel.TemporarySuspension, endsAt: NextWeek, penalty: 1_000_000m), Admin);

        var sanction = Assert.Single(store.Sanctions);
        Assert.True(sanction.IsOwnerLock);
        Assert.Equal(SanctionLevel.TemporarySuspension, sanction.Level);
        Assert.Equal(NextWeek, sanction.EndsAtUtc);
        Assert.Equal(1_000_000m, sanction.PenaltyAmount);
        Assert.Equal(OwnerTsnUser, sanction.OwnerUserId);
        Assert.Equal(SanctionSyncStatus.Synced, sanction.UserServiceSyncStatus);
        Assert.Equal(SanctionSyncStatus.Synced, sanction.NotificationSyncStatus);    // US-097 báo chủ bãi; US-096 thì không
        Assert.Equal(SanctionSyncStatus.NotRequired, sanction.ParkingSyncStatus);
        Assert.Equal((OwnerTsn, "Từ chối 5 booking trong tuần", (DateTime?)NextWeek), Assert.Single(users.LockCalls));
        var audit = Assert.Single(store.AuditLogs);
        Assert.Equal(("Owner.Locked", "OwnerProfile"), (audit.Action, audit.EntityName));   // cùng audit với US-096
        Assert.Equal(0, lots.Calls);
        Assert.Equal("TemporarySuspension", Assert.Single(result.Sanctions).Level);
    }

    [Fact]
    public async Task Owner_lock_sync_failure_keeps_the_sanction_with_failed_status()
    {
        var users = new FakeOwnerDirectoryUserService(TsnOwner) { LockFailure = new DependencyUnavailableException("UserService down") };
        var (useCase, store, _, _) = Create(users: users);

        var result = await useCase.ExecuteAsync(Request(SanctionLevel.PermanentBan), Admin);

        Assert.Equal(SanctionSyncStatus.Failed, Assert.Single(store.Sanctions).UserServiceSyncStatus);
        Assert.Equal("Failed", Assert.Single(result.Sanctions).UserServiceSyncStatus);
    }

    [Fact]
    public async Task Permanent_ban_supersedes_an_active_temporary_lock_in_one_step()
    {
        var temporary = OwnerLock(20, SanctionLevel.TemporarySuspension, NextWeek);
        var (useCase, store, users, _) = Create(new InMemorySanctionStore([temporary]));

        var result = await useCase.ExecuteAsync(Request(SanctionLevel.PermanentBan, reason: "Tái phạm sau tạm khóa"), Admin);

        Assert.Equal(SanctionStatus.Revoked, temporary.Status);
        Assert.Equal(NowUtc, temporary.RevokedAtUtc);
        Assert.Equal(Admin, temporary.RevokedByUserId);
        Assert.StartsWith("Nâng lên cấp 4", temporary.RevokeReason);
        Assert.Equal(SanctionSyncStatus.NotRequired, temporary.UserServiceSyncStatus);   // không cần mở khóa – khóa mới ghi đè
        var ban = Assert.Single(store.Sanctions, s => s.Status == SanctionStatus.Active);
        Assert.Equal(SanctionLevel.PermanentBan, ban.Level);
        Assert.Null(ban.EndsAtUtc);
        Assert.Null(Assert.Single(users.LockCalls).LockedUntilUtc);
        using var oldValues = JsonDocument.Parse(Assert.Single(store.AuditLogs).OldValuesJson!);
        Assert.Equal(20, oldValues.RootElement.GetProperty("sanctionId").GetInt32());
        Assert.Equal("PermanentBan", Assert.Single(result.Sanctions).Level);
    }

    [Theory]
    [InlineData(SanctionLevel.TemporarySuspension, SanctionLevel.TemporarySuspension)]
    [InlineData(SanctionLevel.PermanentBan, SanctionLevel.TemporarySuspension)]
    [InlineData(SanctionLevel.PermanentBan, SanctionLevel.PermanentBan)]
    public async Task Owner_lock_while_another_lock_is_in_effect_is_conflict(SanctionLevel existingLevel, SanctionLevel requested)
    {
        var existing = OwnerLock(20, existingLevel, NextWeek);
        var (useCase, store, users, _) = Create(new InMemorySanctionStore([existing]));
        var request = requested == SanctionLevel.PermanentBan ? Request(requested) : Request(requested, endsAt: NextWeek);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(request, Admin));

        Assert.Contains("#20", ex.Message);
        Assert.Equal(SanctionStatus.Active, existing.Status);
        Assert.Empty(users.LockCalls);
    }

    [Fact]
    public async Task Permanent_ban_cannot_target_lots_or_have_an_end()
    {
        var (useCase, _, _, _) = Create();

        var lots = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(Request(SanctionLevel.PermanentBan, [LotBenThanh]), Admin));
        var ends = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(Request(SanctionLevel.PermanentBan, endsAt: NextWeek), Admin));

        Assert.Contains("toàn bộ chủ bãi", lots.Message);
        Assert.Contains("vĩnh viễn", ends.Message);
    }

    // ===== RiskFlag =====

    [Fact]
    public async Task Linked_open_risk_flag_is_resolved_with_the_sanction_of_its_lot()
    {
        var flag = new RiskFlag
        {
            SubjectType = RiskSubjectType.ParkingLot, SubjectId = LotTanSonNhat, RuleCode = "BOOKING_REJECT", Severity = RiskSeverity.Warning,
            Status = RiskFlagStatus.Investigating, Description = "Từ chối booking", DueAtUtc = NowUtc,
        }.WithId(31);
        var store = new InMemorySanctionStore(riskFlags: [flag]);
        var (useCase, _, _, _) = Create(store);

        await useCase.ExecuteAsync(Request(SanctionLevel.Warning, [LotBenThanh, LotTanSonNhat], correctionDue: NextWeek, riskFlagId: 31), Admin);

        Assert.Equal(store.Sanctions.Single(s => s.ParkingLotId == LotTanSonNhat).Id, flag.SanctionId);
        Assert.Equal(RiskFlagStatus.Resolved, flag.Status);
        Assert.Equal(NowUtc, flag.ResolvedAtUtc);
    }

    [Fact]
    public async Task Risk_flag_of_another_subject_or_already_handled_is_rejected()
    {
        var foreign = new RiskFlag { SubjectType = RiskSubjectType.OwnerProfile, SubjectId = OwnerVincom, RuleCode = "R", Description = "d" }.WithId(1);
        var handled = new RiskFlag { SubjectType = RiskSubjectType.OwnerProfile, SubjectId = OwnerTsn, RuleCode = "R", Description = "d",
            Status = RiskFlagStatus.Dismissed }.WithId(2);
        var (useCase, store, _, _) = Create(new InMemorySanctionStore(riskFlags: [foreign, handled]));

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(Request(SanctionLevel.Warning, correctionDue: NextWeek, riskFlagId: 1), Admin));
        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(Request(SanctionLevel.Warning, correctionDue: NextWeek, riskFlagId: 2), Admin));
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(Request(SanctionLevel.Warning, correctionDue: NextWeek, riskFlagId: 99), Admin));
        Assert.Empty(store.Sanctions);
    }

    // ===== Đầu vào chung =====

    [Fact]
    public async Task Owner_user_must_own_the_owner_profile()
    {
        var (useCase, store, _, _) = Create();

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(
            Request(SanctionLevel.Warning, correctionDue: NextWeek) with { OwnerUserId = 404 }, Admin));
        var mismatch = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(
            Request(SanctionLevel.Warning, correctionDue: NextWeek) with { OwnerUserId = OwnerVincomUser }, Admin));

        Assert.Contains("không sở hữu", mismatch.Message);
        Assert.Empty(store.Sanctions);
    }

    [Theory]
    [InlineData(null, "level")]
    [InlineData((SanctionLevel)9, "level")]
    public async Task Level_is_required_and_known(SanctionLevel? level, string message)
    {
        var (useCase, _, _, _) = Create();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(Request(level, correctionDue: NextWeek), Admin));

        Assert.Contains(message, ex.Message);
    }

    [Theory]
    [InlineData("   ", "không được để trống")]
    [InlineData(null, "không được để trống")]
    public async Task Reason_is_required(string? reason, string message)
    {
        var (useCase, _, _, _) = Create();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(Request(SanctionLevel.Warning, correctionDue: NextWeek, reason: reason), Admin));

        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public async Task Invalid_penalty_evidence_and_lot_ids_are_rejected()
    {
        var (useCase, _, _, _) = Create();
        Task Issue(IssueSanctionRequest r) => useCase.ExecuteAsync(r, Admin);

        await Assert.ThrowsAsync<ValidationException>(() => Issue(Request(SanctionLevel.Warning, correctionDue: NextWeek, penalty: -1)));
        await Assert.ThrowsAsync<ValidationException>(() => Issue(Request(SanctionLevel.Warning, correctionDue: NextWeek, penalty: 1.005m)));
        await Assert.ThrowsAsync<ValidationException>(() => Issue(Request(SanctionLevel.Warning, correctionDue: NextWeek, evidence: "{not json")));
        await Assert.ThrowsAsync<ValidationException>(() => Issue(Request(SanctionLevel.Warning, [0], correctionDue: NextWeek)));
        await Assert.ThrowsAsync<ValidationException>(() => Issue(Request(SanctionLevel.Warning, Enumerable.Range(1, 51).ToArray(), correctionDue: NextWeek)));
        await Assert.ThrowsAsync<ValidationException>(() => Issue(Request(SanctionLevel.RankingPenalty, [LotBenThanh], endsAt: NextWeek, correctionDue: NextWeek)));
    }
}
