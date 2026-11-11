using AdminService.Application.Features.Sanctions;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using static AdminService.Test.SanctionFixtures;

namespace AdminService.Test;

// US-097: danh sách / chi tiết / KPI / gợi ý cấp – chạy đúng biểu thức của SanctionQueryFilter trên LINQ in-memory.
public class SanctionQueryTests
{
    private static readonly DateTime NextWeek = NowUtc.AddDays(7);

    private static InMemorySanctionStore Store()
    {
        var warning = LotSanction(1, LotBenThanh, SanctionLevel.Warning, NowUtc.AddDays(3), startsAtUtc: NowUtc.AddDays(-10));
        warning.Reason = "Phản hồi khiếu nại quá 48 giờ";
        var ranking = LotSanction(2, LotTanSonNhat, SanctionLevel.RankingPenalty, NowUtc.AddDays(20), startsAtUtc: NowUtc.AddDays(-5));
        ranking.ParkingSyncStatus = SanctionSyncStatus.Failed;
        var staleSuspension = LotSanction(3, LotBenThanh, SanctionLevel.TemporarySuspension, NowUtc.AddHours(-1), startsAtUtc: NowUtc.AddDays(-3));
        var revoked = LotSanction(4, LotVincom, SanctionLevel.RankingPenalty, NextWeek, ownerProfileId: OwnerVincom,
            status: SanctionStatus.Revoked, startsAtUtc: NowUtc.AddDays(-2));
        var ban = OwnerLock(5, SanctionLevel.PermanentBan, null, ownerProfileId: OwnerVincom);
        ban.NotificationSyncStatus = SanctionSyncStatus.Pending;
        return new InMemorySanctionStore([warning, ranking, staleSuspension, revoked, ban]);
    }

    private static Task<ParkingManagement.SharedKernel.Contracts.PagedResult<SanctionSummaryDto>> List(SanctionListQuery query, int page = 1, int pageSize = 20)
        => new ListSanctionsUseCase(Store(), new FixedTime(Now)).ExecuteAsync(query, page, pageSize);

    [Fact]
    public async Task Active_filter_only_returns_sanctions_still_in_effect()
    {
        var result = await List(new SanctionListQuery(Status: SanctionStatus.Active));

        Assert.Equal([1, 2, 5], result.Items.Select(s => s.Id).Order());     // #3 đã qua EndsAt, #4 đã gỡ
        Assert.All(result.Items, s => Assert.Equal("Active", s.Status));
    }

    [Fact]
    public async Task Active_sanction_past_its_end_is_listed_as_expired()
    {
        var result = await List(new SanctionListQuery(Status: SanctionStatus.Expired));

        Assert.Equal("Expired", Assert.Single(result.Items).Status);
        Assert.Equal(3, result.Items[0].Id);
    }

    [Fact]
    public async Task Filters_combine_level_owner_lot_search_dates_and_sync_failed()
    {
        Assert.Equal([2, 4], (await List(new SanctionListQuery(Level: SanctionLevel.RankingPenalty))).Items.Select(s => s.Id).Order());
        Assert.Equal([1, 2, 3], (await List(new SanctionListQuery(OwnerProfileId: OwnerTsn))).Items.Select(s => s.Id).Order());
        Assert.Equal([1, 3], (await List(new SanctionListQuery(ParkingLotId: LotBenThanh))).Items.Select(s => s.Id).Order());
        Assert.Equal([1], (await List(new SanctionListQuery(Search: "KHIẾU NẠI"))).Items.Select(s => s.Id));
        Assert.Equal([3], (await List(new SanctionListQuery(Search: "#3"))).Items.Select(s => s.Id));
        // Số khớp Id chế tài (#5) hoặc Id bãi (bãi 5 của #2).
        Assert.Equal([2, 5], (await List(new SanctionListQuery(Search: "5"))).Items.Select(s => s.Id).Order());
        Assert.Equal([2], (await List(new SanctionListQuery(SyncFailed: true))).Items.Select(s => s.Id));
        Assert.Equal([2, 3], (await List(new SanctionListQuery(From: NowUtc.AddDays(-6), To: NowUtc.AddDays(-2.5)))).Items.Select(s => s.Id).Order());
    }

    [Fact]
    public async Task List_is_newest_first_and_paged()
    {
        var result = await List(new SanctionListQuery(), page: 2, pageSize: 2);

        Assert.Equal((2, 2, 5), (result.Page, result.PageSize, result.TotalCount));
        Assert.Equal([3, 2], result.Items.Select(s => s.Id));                  // #5 (−2 ngày, Id lớn) và #4 ở trang 1
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 101)]
    public async Task Invalid_paging_is_rejected(int page, int pageSize)
        => await Assert.ThrowsAsync<ValidationException>(() => List(new SanctionListQuery(), page, pageSize));

    [Fact]
    public async Task Invalid_filters_are_rejected()
    {
        await Assert.ThrowsAsync<ValidationException>(() => List(new SanctionListQuery(From: NowUtc, To: NowUtc)));
        await Assert.ThrowsAsync<ValidationException>(() => List(new SanctionListQuery(OwnerProfileId: 0)));
        await Assert.ThrowsAsync<ValidationException>(() => List(new SanctionListQuery(Search: new string('a', 101))));
    }

    [Fact]
    public async Task Stats_count_in_effect_levels_sync_problems_and_ending_within_7_days()
    {
        var stats = await new GetSanctionStatsUseCase(Store(), new FixedTime(Now)).ExecuteAsync();

        Assert.Equal(new SanctionStatsDto(ActiveTotal: 3, Warning: 1, RankingPenalty: 1, TemporarySuspension: 0, PermanentBan: 1,
            SyncFailed: 1, SyncPending: 1, EndingWithin7Days: 1), stats);
    }

    [Fact]
    public async Task Detail_of_missing_sanction_is_404()
        => await Assert.ThrowsAsync<NotFoundException>(() => new GetSanctionUseCase(Store(), new FixedTime(Now)).ExecuteAsync(42));

    [Fact]
    public async Task Owner_lock_timeline_only_includes_lock_audits_before_the_next_lock()
    {
        var first = OwnerLock(10, SanctionLevel.TemporarySuspension, NextWeek).CreatedAt(NowUtc.AddDays(-20));
        first.Status = SanctionStatus.Expired;
        var second = OwnerLock(11, SanctionLevel.PermanentBan, null).CreatedAt(NowUtc.AddDays(-1));
        AuditLog Audit(int id, string action, DateTime at, string entityName = "OwnerProfile", string entityId = "2")
            => new AuditLog { Action = action, EntityName = entityName, EntityId = entityId, SourceService = "AdminService" }.WithId(id).CreatedAt(at);
        var store = new InMemorySanctionStore([first, second], audits:
        [
            Audit(1, "Owner.Locked", NowUtc.AddDays(-20)),
            Audit(2, "Owner.Unlocked", NowUtc.AddDays(-15)),
            Audit(3, "Owner.Locked", NowUtc.AddDays(-1)),
            Audit(4, "Sanction.Issued", NowUtc.AddDays(-1), "Sanction", "99"),      // chế tài khác
        ]);

        var detail = await new GetSanctionUseCase(store, new FixedTime(Now)).ExecuteAsync(10);

        Assert.Equal([1, 2], detail.Timeline.Select(t => t.AuditLogId));
        Assert.Equal([3], (await new GetSanctionUseCase(store, new FixedTime(Now)).ExecuteAsync(11)).Timeline.Select(t => t.AuditLogId));
    }

    // ===== Gợi ý cấp =====

    private static Task<SanctionSuggestionDto> Suggest(InMemorySanctionStore store, int ownerProfileId = OwnerTsn)
        => new GetSanctionSuggestionUseCase(store, new SanctionSettings(SuggestionWindowDays: 90), new FixedTime(Now)).ExecuteAsync(ownerProfileId);

    [Fact]
    public async Task Suggestion_starts_with_warning_when_no_recent_sanction()
    {
        var old = LotSanction(1, LotBenThanh, SanctionLevel.TemporarySuspension, NowUtc.AddDays(-80), startsAtUtc: NowUtc.AddDays(-91));

        var suggestion = await Suggest(new InMemorySanctionStore([old]));

        Assert.Equal("Warning", suggestion.SuggestedLevel);
        Assert.Empty(suggestion.Basis);
        Assert.Equal(90, suggestion.WindowDays);
    }

    [Fact]
    public async Task Suggestion_escalates_one_level_above_highest_non_revoked_in_window()
    {
        var warning = LotSanction(1, LotBenThanh, SanctionLevel.Warning, NowUtc.AddDays(-30), startsAtUtc: NowUtc.AddDays(-40));
        var ranking = LotSanction(2, LotTanSonNhat, SanctionLevel.RankingPenalty, NowUtc.AddDays(-1), startsAtUtc: NowUtc.AddDays(-20));
        var revokedSuspension = LotSanction(3, LotBenThanh, SanctionLevel.TemporarySuspension, NextWeek, status: SanctionStatus.Revoked);

        var suggestion = await Suggest(new InMemorySanctionStore([warning, ranking, revokedSuspension]));

        Assert.Equal("TemporarySuspension", suggestion.SuggestedLevel);
        Assert.Equal([2, 1], suggestion.Basis.Select(s => s.Id));
        Assert.Contains("Hạ thứ hạng", suggestion.Rationale);
    }

    [Fact]
    public async Task Suggestion_stays_at_permanent_ban()
    {
        var suggestion = await Suggest(new InMemorySanctionStore([OwnerLock(5, SanctionLevel.PermanentBan, null)]));

        Assert.Equal("PermanentBan", suggestion.SuggestedLevel);
        Assert.Contains("cấp cao nhất", suggestion.Rationale);
    }

    [Fact]
    public async Task Suggestion_rejects_invalid_owner()
        => await Assert.ThrowsAsync<ValidationException>(() => Suggest(new InMemorySanctionStore(), 0));
}
