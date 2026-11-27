using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.API.Controllers;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.ParkingLots;
using ParkingService.Application.Features.SanctionStates;
using ParkingService.Domain.Entities;
using ParkingService.Domain.Enums;

namespace ParkingService.Test;

/// <summary>
/// US-097: hiệu lực chế tài SLA ở ParkingService – PUT/DELETE idempotent từ AdminService, trạng thái công khai / Admin,
/// và tìm kiếm loại bãi tạm ngưng + đẩy bãi hạ thứ hạng xuống cuối TRƯỚC Take(limit).
/// </summary>
public class SanctionStateTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc);
    private const int Admin = 1;
    private const int OwnerA = 10;
    private const int OwnerB = 20;
    private const double Lat = 10.7770;
    private const double Lng = 106.7010;

    // ===== Tìm kiếm =====

    /// <summary>Bãi thứ <paramref name="rank"/> cách điểm tìm ~111 m × rank (rank nhỏ = gần hơn).</summary>
    private static ParkingLot Lot(int id, int rank, int ownerProfileId = OwnerA, int available = 10)
        => new(id, $"Bãi {id}", "TP.HCM", Lat + rank * 0.001, Lng, 100, available, 210, new TimeOnly(0, 0), new TimeOnly(23, 59),
            ParkingLotStatus.Active, ownerProfileId);

    private static SanctionState LotState(int sanctionId, int lotId, SanctionStateKind kind, int ownerProfileId = OwnerA,
        DateTime? startsAt = null, DateTime? endsAt = null)
        => SanctionState.Create(sanctionId, ownerProfileId, lotId, kind, startsAt ?? Now.AddDays(-1), endsAt ?? Now.AddDays(7), Admin);

    private static SanctionState OwnerState(int sanctionId, int ownerProfileId, SanctionStateKind kind = SanctionStateKind.RankingPenalty)
        => SanctionState.Create(sanctionId, ownerProfileId, null, kind, Now.AddDays(-1), Now.AddDays(7), Admin);

    private static async Task<IReadOnlyList<int>> Search(IEnumerable<ParkingLot> lots, IEnumerable<SanctionState> states, int limit = 20,
        string sortBy = "Distance")
    {
        var lotList = lots.ToList();
        var useCase = new SearchParkingLotsUseCase(new FakeLotRepo(lotList), new FixedClock(Now), new FakeSanctionStore(lotList, states));
        var result = await useCase.ExecuteAsync(new SearchParkingLotsQuery(Lat, Lng, RadiusKm: 5, Limit: limit, SortBy: sortBy));
        return result.Select(r => r.Id).ToList();
    }

    [Fact]
    public async Task Search_hides_suspended_lots_and_still_returns_a_full_limit_of_valid_lots()
    {
        // 6 bãi gần nhất bị tạm ngưng, 4 bãi xa hơn hợp lệ: lọc trước Take nên vẫn trả đủ 3.
        var lots = Enumerable.Range(1, 10).Select(i => Lot(i, rank: i)).ToList();
        var states = Enumerable.Range(1, 6).Select(i => LotState(100 + i, i, SanctionStateKind.ListingSuspension));

        Assert.Equal([7, 8, 9], await Search(lots, states, limit: 3));
    }

    [Fact]
    public async Task Search_fills_limit_with_valid_lots_before_penalized_ones_when_many_nearby_are_sanctioned()
    {
        // Gần → xa: 1–2 tạm ngưng, 3–5 hạ thứ hạng, 6–8 hợp lệ.
        var lots = Enumerable.Range(1, 8).Select(i => Lot(i, rank: i)).ToList();
        var states = new[]
        {
            LotState(101, 1, SanctionStateKind.ListingSuspension), LotState(102, 2, SanctionStateKind.ListingSuspension),
            LotState(103, 3, SanctionStateKind.RankingPenalty), LotState(104, 4, SanctionStateKind.RankingPenalty),
            LotState(105, 5, SanctionStateKind.RankingPenalty),
        };

        Assert.Equal([6, 7, 8], await Search(lots, states, limit: 3));
        Assert.Equal([6, 7, 8, 3, 4], await Search(lots, states, limit: 5));
    }

    [Theory]
    [InlineData("Distance")]
    [InlineData("AvailableSlots")]
    [InlineData("Rating")]
    public async Task Penalized_lots_appear_only_after_all_non_penalized_lots_for_every_sort(string sortBy)
    {
        // Bãi bị hạ thứ hạng là bãi gần nhất VÀ nhiều chỗ nhất – vẫn phải đứng sau.
        var lots = new[]
        {
            Lot(1, rank: 1, available: 90), Lot(2, rank: 2, available: 80),
            Lot(3, rank: 3, available: 5), Lot(4, rank: 4, available: 4), Lot(5, rank: 5, available: 3),
        };
        var states = new[] { LotState(101, 1, SanctionStateKind.RankingPenalty), LotState(102, 2, SanctionStateKind.RankingPenalty) };

        var ids = await Search(lots, states, sortBy: sortBy);

        Assert.Equal([3, 4, 5], ids.Take(3).Order());
        Assert.Equal([1, 2], ids.Skip(3));      // trong nhóm bị hạ, vẫn giữ tiêu chí sắp xếp
    }

    [Fact]
    public async Task Owner_level_ranking_penalty_pushes_down_every_lot_of_that_owner()
    {
        var lots = new[] { Lot(1, rank: 1, OwnerB), Lot(2, rank: 2), Lot(3, rank: 3, OwnerB), Lot(4, rank: 4) };

        Assert.Equal([2, 4, 1, 3], await Search(lots, [OwnerState(201, OwnerB)]));
    }

    [Fact]
    public async Task Lifted_expired_and_future_states_do_not_affect_search()
    {
        var lots = Enumerable.Range(1, 4).Select(i => Lot(i, rank: i)).ToList();
        var lifted = LotState(101, 1, SanctionStateKind.ListingSuspension);
        lifted.Lift(Admin, Now.AddHours(-1));
        var expired = LotState(102, 2, SanctionStateKind.ListingSuspension, endsAt: Now);              // EndsAt = now → hết hiệu lực
        var future = LotState(103, 3, SanctionStateKind.RankingPenalty, startsAt: Now.AddHours(1), endsAt: Now.AddDays(2));

        Assert.Equal([1, 2, 3, 4], await Search(lots, [lifted, expired, future]));
    }

    [Fact]
    public async Task Lot_level_state_matches_by_lot_even_if_owner_changed()
    {
        var lots = new[] { Lot(1, rank: 1, OwnerB), Lot(2, rank: 2) };

        Assert.Equal([2], await Search(lots, [LotState(101, 1, SanctionStateKind.ListingSuspension, ownerProfileId: OwnerA)]));
    }

    // ===== PUT (áp / áp lại) =====

    private static (UpsertSanctionStateUseCase UseCase, FakeSanctionStore Store) Upsert(params SanctionState[] states)
    {
        var store = new FakeSanctionStore([Lot(1, rank: 1), Lot(2, rank: 2, OwnerB)], states);
        return (new UpsertSanctionStateUseCase(store, store, new FixedClock(Now)), store);
    }

    private static UpsertSanctionStateRequest Request(int? lotId = 1, SanctionStateKind? kind = SanctionStateKind.ListingSuspension,
        int ownerProfileId = OwnerA, DateTime? startsAt = null, DateTime? endsAt = null)
        => new(ownerProfileId, lotId, kind, startsAt ?? Now, endsAt ?? Now.AddDays(3));

    [Fact]
    public async Task Upsert_creates_state_in_effect()
    {
        var (useCase, store) = Upsert();

        var dto = await useCase.ExecuteAsync(12, Request(), Admin);

        Assert.Equal(new SanctionStateDto(12, OwnerA, 1, "ListingSuspension", Now, Now.AddDays(3), null, true), dto);
        var state = Assert.Single(store.States);
        Assert.Equal((Admin, 1), (state.AppliedByUserId, store.SaveCount));
    }

    [Fact]
    public async Task Repeated_upsert_for_same_sanction_only_updates_times()
    {
        var (useCase, store) = Upsert();
        await useCase.ExecuteAsync(12, Request(), Admin);

        var dto = await useCase.ExecuteAsync(12, Request(endsAt: Now.AddDays(5)), 7);

        Assert.Equal(Now.AddDays(5), dto.EndsAtUtc);
        Assert.Equal(7, Assert.Single(store.States).AppliedByUserId);
    }

    [Fact]
    public async Task Upsert_of_lifted_or_mismatching_sanction_is_409()
    {
        var lifted = LotState(12, 1, SanctionStateKind.ListingSuspension);
        lifted.Lift(Admin, Now);
        var (useCase, _) = Upsert(lifted, LotState(13, 1, SanctionStateKind.ListingSuspension));

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(12, Request(), Admin));
        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(13, Request(kind: SanctionStateKind.RankingPenalty), Admin));
        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(13, Request(lotId: null, kind: SanctionStateKind.RankingPenalty), Admin));
    }

    public static TheoryData<int, UpsertSanctionStateRequest> InvalidRequests => new()
    {
        { 0, Request() },
        { 12, Request(ownerProfileId: 0) },
        { 12, Request(lotId: 0) },
        { 12, Request(kind: null) },
        { 12, Request(kind: (SanctionStateKind)9) },
        { 12, new UpsertSanctionStateRequest(OwnerA, 1, SanctionStateKind.ListingSuspension, null, null) },
        { 12, Request(endsAt: Now) },                                                   // kết thúc ≤ bắt đầu
        { 12, new UpsertSanctionStateRequest(OwnerA, 1, SanctionStateKind.RankingPenalty, Now, null) },   // hạ hạng phải có hạn
        { 12, Request(lotId: null) },                                                   // tạm ngưng phải chỉ định bãi
        { 12, Request(lotId: 2) },                                                      // bãi 2 thuộc OwnerB
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Invalid_upsert_is_400(int sanctionId, UpsertSanctionStateRequest request)
    {
        var (useCase, store) = Upsert();

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(sanctionId, request, Admin));
        Assert.Empty(store.States);
    }

    [Fact]
    public async Task Upsert_on_missing_lot_is_404()
        => await Assert.ThrowsAsync<NotFoundException>(() => Upsert().UseCase.ExecuteAsync(12, Request(lotId: 99), Admin));

    [Fact]
    public async Task Upsert_owner_level_ranking_penalty_and_treat_unspecified_times_as_utc()
    {
        var (useCase, _) = Upsert();
        var naive = DateTime.SpecifyKind(Now, DateTimeKind.Unspecified);

        var dto = await useCase.ExecuteAsync(12, new UpsertSanctionStateRequest(OwnerB, null, SanctionStateKind.RankingPenalty,
            naive, naive.AddDays(10)), Admin);

        Assert.Null(dto.ParkingLotId);
        Assert.Equal(DateTimeKind.Utc, dto.StartsAtUtc.Kind);
        Assert.Equal(Now, dto.StartsAtUtc);
    }

    // ===== DELETE (gỡ) =====

    [Fact]
    public async Task Lift_is_idempotent_and_keeps_first_lift_time()
    {
        var state = LotState(12, 1, SanctionStateKind.ListingSuspension);
        var store = new FakeSanctionStore([], [state]);

        await new LiftSanctionStateUseCase(store, new FixedClock(Now)).ExecuteAsync(12, Admin);
        await new LiftSanctionStateUseCase(store, new FixedClock(Now.AddHours(1))).ExecuteAsync(12, 7);
        await new LiftSanctionStateUseCase(store, new FixedClock(Now)).ExecuteAsync(99, Admin);   // chưa từng áp → không lỗi

        Assert.Equal((Now, Admin), (state.LiftedAtUtc!.Value, state.LiftedByUserId!.Value));
        Assert.Equal(1, store.SaveCount);
        Assert.False(state.IsInEffect(Now));
        await Assert.ThrowsAsync<ValidationException>(() => new LiftSanctionStateUseCase(store, new FixedClock(Now)).ExecuteAsync(0, Admin));
    }

    // ===== Trạng thái công khai / Admin =====

    private static FakeSanctionStore StatusStore(params SanctionState[] states)
        => new([Lot(1, rank: 1), Lot(2, rank: 2), Lot(3, rank: 3, OwnerB)], states);

    private static Task<LotSanctionStatusDto> Status(FakeSanctionStore store, int lotId)
        => new GetLotSanctionStatusUseCase(store, new FixedClock(Now)).ExecuteAsync(lotId);

    [Fact]
    public async Task Public_status_reports_listing_suspension_with_latest_end()
    {
        var store = StatusStore(LotState(101, 1, SanctionStateKind.ListingSuspension, endsAt: Now.AddDays(2)),
            LotState(102, 1, SanctionStateKind.ListingSuspension, endsAt: Now.AddDays(5)));

        Assert.Equal(new LotSanctionStatusDto(true, Now.AddDays(5)), await Status(store, 1));
        Assert.Equal(new LotSanctionStatusDto(false, null), await Status(store, 2));
    }

    [Fact]
    public async Task Public_status_does_not_reveal_ranking_penalty()
    {
        var store = StatusStore(LotState(101, 1, SanctionStateKind.RankingPenalty), OwnerState(102, OwnerB));

        Assert.Equal(new LotSanctionStatusDto(false, null), await Status(store, 1));
        Assert.Equal(new LotSanctionStatusDto(false, null), await Status(store, 3));
    }

    [Fact]
    public async Task Indefinite_suspension_has_no_end()
    {
        var store = StatusStore(SanctionState.Create(101, OwnerA, 1, SanctionStateKind.ListingSuspension, Now.AddDays(-1), null, Admin),
            LotState(102, 1, SanctionStateKind.ListingSuspension));

        Assert.Equal(new LotSanctionStatusDto(true, null), await Status(store, 1));
    }

    [Fact]
    public async Task Status_of_missing_or_invalid_lot_is_404_or_400()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Status(StatusStore(), 99));
        await Assert.ThrowsAsync<ValidationException>(() => Status(StatusStore(), 0));
    }

    [Fact]
    public async Task Admin_state_shows_suspension_ranking_and_owner_level_states()
    {
        var store = StatusStore(LotState(101, 3, SanctionStateKind.ListingSuspension, ownerProfileId: OwnerB, endsAt: Now.AddDays(4)),
            OwnerState(102, OwnerB), LotState(103, 1, SanctionStateKind.RankingPenalty));

        var state = await new GetLotSanctionStateUseCase(store, new FixedClock(Now)).ExecuteAsync(3);

        Assert.Equal((3, OwnerB, true, (DateTime?)Now.AddDays(4), true, (DateTime?)Now.AddDays(7)),
            (state.ParkingLotId, state.OwnerProfileId, state.IsListingSuspended, state.ListingSuspendedUntilUtc,
                state.IsRankingPenalized, state.RankingPenaltyUntilUtc));
        Assert.Equal([101, 102], state.ActiveStates.Select(s => s.SanctionId));
    }

    // ===== Controller =====

    [Fact]
    public void Admin_endpoints_require_admin_and_public_status_is_anonymous()
    {
        var admin = typeof(AdminSanctionStatesController);
        Assert.Equal("RequireAdmin", admin.GetCustomAttribute<AuthorizeAttribute>()!.Policy);
        Assert.Equal("api/v1/parking-lots", admin.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("sanction-states/{sanctionId:int}",
            admin.GetMethod(nameof(AdminSanctionStatesController.Upsert))!.GetCustomAttribute<HttpPutAttribute>()!.Template);
        Assert.Equal("sanction-states/{sanctionId:int}",
            admin.GetMethod(nameof(AdminSanctionStatesController.Lift))!.GetCustomAttribute<HttpDeleteAttribute>()!.Template);
        Assert.Equal("{lotId:int}/sanction-state",
            admin.GetMethod(nameof(AdminSanctionStatesController.GetLotState))!.GetCustomAttribute<HttpGetAttribute>()!.Template);

        var status = typeof(LotSanctionStatusController);
        Assert.Null(status.GetCustomAttribute<AuthorizeAttribute>(inherit: true));
        Assert.Null(status.GetMethod(nameof(LotSanctionStatusController.Get))!.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/v1/parking-lots/{lotId:int}/sanction-status", status.GetCustomAttribute<RouteAttribute>()!.Template);
        // Hợp đồng công khai chỉ có 2 trường.
        Assert.Equal(["IsListingSuspended", "ListingSuspendedUntilUtc"],
            typeof(LotSanctionStatusDto).GetProperties().Select(p => p.Name).Order());
    }

    // ===== Test doubles =====

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class FakeLotRepo(List<ParkingLot> lots) : IParkingLotRepository
    {
        public Task<ParkingLot?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(lots.FirstOrDefault(l => l.Id == id));

        public Task<IReadOnlyList<ParkingLot>> FindActiveInBoxAsync(double minLat, double maxLat, double minLng, double maxLng,
            int minHeightCm, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ParkingLot>>(lots.Where(l => l.Status == ParkingLotStatus.Active).ToList());
    }

    /// <summary>Bộ nhớ trong cho cả cổng đọc và ghi; lọc hiệu lực bằng đúng biểu thức Domain mà EF dịch sang SQL.</summary>
    private sealed class FakeSanctionStore(IEnumerable<ParkingLot> lots, IEnumerable<SanctionState> states)
        : ISanctionStateQueries, ISanctionStateRepository
    {
        private readonly List<ParkingLot> lots = lots.ToList();
        public List<SanctionState> States { get; } = states.ToList();
        public int SaveCount { get; private set; }

        public Task<SanctionLotRow?> FindLotAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult(lots.Where(l => l.Id == parkingLotId).Select(l => new SanctionLotRow(l.Id, l.OwnerProfileId)).FirstOrDefault());

        public Task<IReadOnlyList<SanctionState>> ListInEffectAsync(IReadOnlyCollection<int> parkingLotIds,
            IReadOnlyCollection<int> ownerProfileIds, DateTime nowUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SanctionState>>(States.AsQueryable()
                .Where(SanctionState.InEffectAt(nowUtc))
                .Where(s => (s.ParkingLotId != null && parkingLotIds.Contains(s.ParkingLotId.Value))
                            || (s.ParkingLotId == null && ownerProfileIds.Contains(s.OwnerProfileId)))
                .ToList());

        public Task<SanctionState?> FindBySanctionIdAsync(int sanctionId, CancellationToken cancellationToken = default)
            => Task.FromResult(States.FirstOrDefault(s => s.SanctionId == sanctionId));

        public void Add(SanctionState state) => States.Add(state);

        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
