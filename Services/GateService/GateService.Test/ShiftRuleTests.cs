using System.Linq.Expressions;
using GateService.Application.Features.GateConsole;
using GateService.Application.Features.LotCapacity;
using GateService.Application.Features.Shifts;
using GateService.Domain.Entities;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Test;

/// <summary>US-076: phân quyền theo bãi, vòng đời ca, két tiền và bàn giao. Repository trong bộ nhớ.</summary>
public class ShiftRuleTests
{
    private static readonly DateTime Now = new(2026, 11, 30, 7, 0, 0, DateTimeKind.Utc);
    private const int Lot = 1, OwnerProfile = 5, Owner = 50, An = 11, Binh = 12;
    private readonly Repo repo = new();
    private readonly Cash cash = new();
    private readonly Clock clock = new();
    private readonly User user = new();
    private readonly CurrentShift current = new();

    private GateAccess Access => new(user, new Lots(), repo, current);
    private ShiftDutyUseCases Duty => new(Access, repo, cash, new Status(), ShiftOptions.Default, clock);
    private ShiftPlanningUseCases Planning => new(Access, repo, new Staff(), new Lots(), clock);
    private ShiftLifecycleUseCases Lifecycle => new(repo, cash, ShiftOptions.Default, clock);
    private CashDrawer Drawer => new(current, repo);

    private Shift Add(int staff, int startHour, int endHour, ShiftStatus status = ShiftStatus.Scheduled, bool holder = true)
    {
        var shift = new Shift
        {
            ParkingLotId = Lot, OwnerProfileId = OwnerProfile, StaffUserId = staff, IsCashHolder = holder, Status = status,
            ScheduledStartUtc = Now.AddHours(startHour), ScheduledEndUtc = Now.AddHours(endHour),
            StartedAtUtc = status == ShiftStatus.Open ? Now.AddHours(startHour) : null,
            BusinessDate = Shift.BusinessDateOf(Now.AddHours(startHour)),
        };
        repo.Add(shift);
        return shift;
    }

    [Fact]
    public async Task Cash_left_in_the_drawer_becomes_the_next_holders_opening_cash()
    {
        var an = Add(An, -8, 0, ShiftStatus.Open);
        var binh = Add(Binh, 0, 8);
        cash.Totals[an.Id] = 1_230_000;

        user.Staff(An);
        var ended = await Duty.EndAsync(an.Id, new(1_230_000, null), default);
        Assert.Equal((ShiftStatus.HandoverPending, binh.Id, (ShiftDiscrepancyStatus?)null), (ended.Status, ended.HandoverToShiftId!.Value, ended.DiscrepancyStatus));

        user.Staff(Binh);
        await Duty.StartAsync(binh.Id, default);
        current.Value = binh;
        var blocked = await Assert.ThrowsAsync<ConflictException>(() => Drawer.AuthorizeCollectionAsync(default));
        Assert.Equal(ShiftErrors.HandoverNotAccepted, blocked.Code);
        Assert.Equal(ShiftErrors.HandoverNotAccepted,
            (await Assert.ThrowsAsync<ConflictException>(() => Duty.EndAsync(binh.Id, new(0, null), default))).Code);

        await Duty.AcceptHandoverAsync(an.Id, new(1_230_000), default);
        Assert.Equal(binh.Id, await Drawer.AuthorizeCollectionAsync(default));
        cash.Totals[binh.Id] = 500_000;
        clock.Value = Now.AddHours(8);
        var done = await Duty.EndAsync(binh.Id, new(1_730_000, null), default);
        Assert.Null(done.DiscrepancyStatus); // 1.230.000 + 500.000
        Assert.Null(done.HandoverToShiftId);
        Assert.Equal(ShiftStatus.Completed, an.Status);
    }

    [Fact]
    public async Task Owner_taking_the_cash_does_not_count_as_next_opening_cash_and_mismatch_is_raised()
    {
        var an = Add(An, -8, 0, ShiftStatus.Open);
        var binh = Add(Binh, 0, 8);
        cash.Totals[an.Id] = 100_000;
        user.Staff(An);
        await Duty.EndAsync(an.Id, new(100_000, null), default);

        user.Owner();
        var accepted = await Duty.AcceptHandoverAsync(an.Id, new(90_000), default);

        Assert.Null(accepted.HandoverToShiftId);
        Assert.Equal(ShiftDiscrepancyStatus.Open, accepted.DiscrepancyStatus);
        Assert.Contains(repo.Events, e => e is CashDiscrepancyRaised d && d.Difference == -10_000);
        Assert.Equal(0, await ShiftCash.OpeningAsync(repo, binh.Id, default));
        var resolved = await Planning.ResolveDiscrepancyAsync(an.Id, new("Đã thu bù"), default);
        Assert.Equal(ShiftDiscrepancyStatus.Resolved, resolved.DiscrepancyStatus);
    }

    [Fact]
    public async Task Only_the_cash_holder_collects()
    {
        var helper = Add(Binh, -1, 7, ShiftStatus.Open, holder: false);
        current.Value = helper;
        Assert.Equal(ShiftErrors.NoCashHolder, (await Assert.ThrowsAsync<ConflictException>(() => Drawer.AuthorizeCollectionAsync(default))).Code);
        Add(An, -2, 6, ShiftStatus.Open);
        Assert.Equal(ShiftErrors.NotCashHolder, (await Assert.ThrowsAsync<ConflictException>(() => Drawer.AuthorizeCollectionAsync(default))).Code);
    }

    [Fact]
    public async Task Gate_writes_need_an_open_shift_on_that_lot_and_admin_is_read_only()
    {
        user.Staff(An);
        Assert.Equal(ShiftErrors.NotActive, (await Assert.ThrowsAsync<ConflictException>(() => Access.RequireActiveShiftAsync(Lot, default))).Code);
        Add(An, -1, 7, ShiftStatus.Open);
        Assert.NotNull(await Access.RequireActiveShiftAsync(Lot, default));
        await Assert.ThrowsAsync<ForbiddenException>(() => Access.RequireActiveShiftAsync(2, default)); // bãi không được phân công
        user.Admin();
        await Access.RequireReadAsync(Lot, default);
        await Assert.ThrowsAsync<ForbiddenException>(() => Access.RequireActiveShiftAsync(Lot, default));
        user.UserId = null;
        await Assert.ThrowsAsync<AuthenticationException>(() => Access.RequireReadAsync(Lot, default));
    }

    [Fact]
    public async Task Staff_removed_from_the_lot_can_no_longer_end_the_shift()
    {
        var shift = Add(An, -1, 7, ShiftStatus.Open);
        user.Staff(An);
        user.RemoveLots();
        await Assert.ThrowsAsync<ForbiddenException>(() => Duty.EndAsync(shift.Id, new(0, null), default));
        Assert.Equal(ShiftStatus.Open, shift.Status);
    }

    [Fact]
    public async Task Start_respects_window_and_lot_status()
    {
        var later = Add(An, 1, 5);
        user.Staff(An);
        Assert.Equal(ShiftErrors.StartTooEarly, (await Assert.ThrowsAsync<ConflictException>(() => Duty.StartAsync(later.Id, default))).Code);
        clock.Value = Now.AddMinutes(46);
        user.Staff(Binh);
        await Assert.ThrowsAsync<NotFoundException>(() => Duty.StartAsync(later.Id, default));
        user.Staff(An);
        Assert.Equal(ShiftStatus.Open, (await Duty.StartAsync(later.Id, default)).Status);
    }

    [Fact]
    public async Task Planning_rejects_overlaps_and_unassigned_staff()
    {
        user.Owner();
        await Planning.CreateAsync(Lot, new([new(An, Now.AddHours(1), Now.AddHours(5))]), default);
        async Task<string?> Code(SaveShiftRequest request)
            => ((ParkingManagement.SharedKernel.Exceptions.IHasErrorCode)await Assert.ThrowsAnyAsync<Exception>(
                () => Planning.CreateAsync(Lot, new([request]), default))).Code;
        Assert.Equal(ShiftErrors.Overlap, await Code(new(An, Now.AddHours(2), Now.AddHours(6), false)));
        Assert.Equal(ShiftErrors.CashHolderOverlap, await Code(new(Binh, Now.AddHours(2), Now.AddHours(6))));
        Assert.Equal(ShiftErrors.StaffNotInLot, await Code(new(999, Now.AddHours(10), Now.AddHours(12))));
        // Chủ bãi tự trực không cần phân công.
        await Planning.CreateAsync(Lot, new([new(Owner, Now.AddHours(10), Now.AddHours(12))]), default);
        user.Staff(An);
        await Assert.ThrowsAsync<ForbiddenException>(() => Planning.WeekAsync(Lot, Shift.BusinessDateOf(Now), default));
    }

    [Fact]
    public async Task Job_marks_missed_closes_forgotten_and_fills_cash_when_payment_returns()
    {
        var forgotten = Add(An, -9, -1, ShiftStatus.Open);
        var missed = Add(Binh, -1, 7);
        cash.Down = true;
        await Lifecycle.RunDueAsync(default);
        Assert.Equal((ShiftStatus.AutoClosed, ShiftClosedReason.Forgot, (decimal?)null),
            (forgotten.Status, forgotten.ClosedReason!.Value, forgotten.CashSystem));
        Assert.Equal(ShiftDiscrepancyStatus.Open, forgotten.DiscrepancyStatus);
        Assert.Equal(ShiftStatus.Missed, missed.Status);
        Assert.Contains(repo.Events, e => e is ShiftMissed);

        cash.Down = false;
        cash.Totals[forgotten.Id] = 70_000;
        await Lifecycle.RunDueAsync(default);
        Assert.Equal(70_000, forgotten.CashSystem);
    }

    [Fact]
    public void Coverage_reports_only_uncovered_opening_hours()
    {
        TimeRange R(int from, int to) => new(Now.AddHours(from), Now.AddHours(to));
        var gaps = ShiftCoverage.Gaps([R(0, 4), R(4, 10)], [R(1, 3), R(2, 5), R(8, 12)]);
        Assert.Equal([R(0, 1), R(5, 8)], gaps);
    }

    [Fact]
    public async Task Retried_checkout_with_an_existing_payment_uses_the_frozen_quote()
    {
        var writer = new FakeWriter();
        var settlements = new FakeSettlements { Existing = new("PM-1", "Succeeded", 20_000) };
        writer.Sessions.Add(new ParkingSession
        {
            Code = "PS-9", ParkingLotId = 2, PlateNumber = "51F12345", EntryAtUtc = Now.AddHours(-5), Status = ParkingSessionStatus.Active,
            QuotedSessionFee = 20_000, QuotedPrepaidAmount = 0, QuotedAdditionalDue = 20_000, QuotedAtUtc = Now.AddHours(-4),
        });
        var checkOut = new CheckOutUseCase(new FakeRecognizer(), new FakeBookings(), new FakeExitFees(), settlements, writer,
            new FakeGateEvents(), clock, new GateConsoleOptions(), new AlwaysAdmitLotCapacityGate(), new NoopCapacityPublisher(), Drawer);

        var result = await checkOut.ExecuteAsync(new CheckOutCommand(2, "51F12345", ConfirmFee: true));

        Assert.Empty(settlements.Records); // không thu lần hai
        Assert.Equal((20_000m, Now.AddHours(-4)), (result.Fee!.AdditionalDue, result.Session.ExitAtUtc!.Value));
        Assert.Equal("Completed", result.Session.Status);
    }

    // ===== fakes =====
    private sealed class Clock : TimeProvider
    {
        public DateTime Value = Now;
        public override DateTimeOffset GetUtcNow() => new(Value);
    }

    private sealed class User : ICurrentUser
    {
        public int? UserId { get; set; }
        public string? Email => null;
        public IReadOnlyList<string> Roles { get; private set; } = [];
        public int? OwnerProfileId { get; private set; }
        public IReadOnlyList<int> ParkingLotIds { get; private set; } = [];
        public bool IsAuthenticated => UserId is not null;
        public void RemoveLots() => ParkingLotIds = [];
        public void Staff(int id) => (UserId, Roles, OwnerProfileId, ParkingLotIds) = (id, ["Staff"], OwnerProfile, [Lot]);
        public void Owner() => (UserId, Roles, OwnerProfileId, ParkingLotIds) = (ShiftRuleTests.Owner, ["LotOwner"], OwnerProfile, []);
        public void Admin() => (UserId, Roles, OwnerProfileId, ParkingLotIds) = (1, ["Admin"], null, []);
    }

    private sealed class Lots : ILotDirectory
    {
        public Task<LotInfo> GetAsync(int parkingLotId, CancellationToken ct)
            => Task.FromResult(new LotInfo(parkingLotId, parkingLotId == Lot ? OwnerProfile : 99, "Lot", "Active"));
        public Task<IReadOnlyList<TimeRange>> OpeningHoursAsync(int parkingLotId, int ownerProfileId, DateOnly weekStart, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<TimeRange>>([]);
    }

    private sealed class Staff : IStaffDirectory
    {
        public Task<bool> IsAssignedAsync(int parkingLotId, int staffUserId, CancellationToken ct) => Task.FromResult(staffUserId is An or Binh);
    }

    private sealed class Status : ILotOperationalStatusProvider
    {
        public Task<LotOperationalStatusSnapshot?> GetAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult<LotOperationalStatusSnapshot?>(new(parkingLotId, "Lot", "Active", false, null));
    }

    private sealed class Cash : IShiftCashSource
    {
        public Dictionary<int, decimal> Totals = [];
        public bool Down;
        public Task<decimal> CashTotalAsync(int shiftId, CancellationToken ct)
            => Down ? throw new DependencyUnavailableException("down") : Task.FromResult(Totals.GetValueOrDefault(shiftId));
    }

    private sealed class Repo : IShiftRepository
    {
        private readonly List<Shift> items = [];
        private readonly List<ShiftTemplate> templates = [];
        public List<IntegrationEvent> Events = [];
        private int next = 1;
        public Task<Shift?> FindAsync(int id, CancellationToken ct) => Task.FromResult(items.FirstOrDefault(s => s.Id == id));
        public Task<List<Shift>> WhereAsync(Expression<Func<Shift, bool>> predicate, CancellationToken ct)
            => Task.FromResult(items.Where(predicate.Compile()).ToList());
        public Task<(IReadOnlyList<Shift> Items, int Total)> PageAsync(Expression<Func<Shift, bool>> predicate, int page, int pageSize, CancellationToken ct)
        {
            var all = items.Where(predicate.Compile()).ToList();
            return Task.FromResult<(IReadOnlyList<Shift>, int)>((all, all.Count));
        }
        public void Add(Shift shift)
        {
            typeof(ParkingManagement.SharedKernel.Domain.BaseEntity).GetProperty("Id")!.SetValue(shift, next++);
            items.Add(shift);
        }
        public void Remove(Shift shift) => items.Remove(shift);
        public Task<List<ShiftTemplate>> TemplatesAsync(int parkingLotId, CancellationToken ct) => Task.FromResult(templates);
        public Task<ShiftTemplate?> FindTemplateAsync(int id, CancellationToken ct) => Task.FromResult(templates.FirstOrDefault(t => t.Id == id));
        public void Add(ShiftTemplate template) => templates.Add(template);
        public void Remove(ShiftTemplate template) => templates.Remove(template);
        public Task<ShiftEventCounts> CountEventsAsync(int shiftId, CancellationToken ct) => Task.FromResult(new ShiftEventCounts(0, 0, 0, 0, 0));
        public Task<int> CountVehiclesInLotAsync(int parkingLotId, CancellationToken ct) => Task.FromResult(0);
        public Task<int> CountPendingPmsAsync(int parkingLotId, CancellationToken ct) => Task.FromResult(0);
        public void Enqueue(IntegrationEvent integrationEvent) => Events.Add(integrationEvent);
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
