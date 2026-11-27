using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;
using SupportService.Application.Services;
using SupportService.Domain.Entities;
using SupportService.Domain.Rules;

namespace SupportService.Test;

public class DisputeResolutionServiceTests
{
    /// <summary>Thứ Sáu 06/11/2026 10:00 giờ Việt Nam.</summary>
    private static readonly DateTime Now = new(2026, 11, 6, 3, 0, 0, DateTimeKind.Utc);
    private const int Driver = 5;
    private const int Owner = 7;
    private const int OwnerUser = 70;
    private const int OtherOwner = 8;
    private const int Lot = 1;
    private const int OtherOwnerLot = 2;

    private static Complaint Ticket(string code = "CP-1", ComplaintStatus status = ComplaintStatus.AwaitingOwner,
        ComplaintCategory category = ComplaintCategory.SlotOccupied, string? bookingCode = "BK-1", DateTime? createdAt = null,
        int? owner = Owner, int lot = Lot)
    {
        var complaint = new Complaint
        {
            Code = code, UserId = Driver, BookingCode = bookingCode, ParkingLotId = lot, OwnerProfileId = owner, Category = category,
            Description = "Chỗ A-05 đã đặt bị xe khác đậu.", Status = status, RequestedAmount = 10000,
            OwnerResponseDueAtUtc = Now.AddHours(40), PayoutHeld = owner is not null && ComplaintRules.IsOpen(status),
        };
        SetCreatedAt(complaint, createdAt ?? Now.AddHours(-2));
        complaint.Messages.Add(new ComplaintMessage { SenderParty = ComplaintParty.System, Message = "Đã tiếp nhận khiếu nại." });
        return complaint;
    }

    private static BookingEvidenceDto Booking(decimal paid = 50000) => new(10, "BK-1", "Confirmed", "51F12345", "Bãi Vincom", "A-05",
        Now.AddHours(-1), Now.AddHours(2), paid, paid,
        [new BookingStatusChangeDto("PendingPayment", "Confirmed", null, "Đã thanh toán", Now.AddDays(-1))]);

    private sealed record Sut(DisputeResolutionService Service, InMemoryComplaintRepository Repo, FakeEvidence Evidence,
        FakeFines Fines, MutableClock Clock);

    private static Sut Create(params Complaint[] tickets)
    {
        var repo = new InMemoryComplaintRepository();
        repo.Saved.AddRange(tickets);
        var evidence = new FakeEvidence { Booking = Booking() };
        var fines = new FakeFines();
        fines.Rates[Lot] = new LotFineRate { ParkingLotId = Lot, OwnerProfileId = Owner, WrongSlotFine = 200000 };
        var clock = new MutableClock(Now);
        return new Sut(new DisputeResolutionService(repo, evidence, evidence, new FakeOwnerLots(), fines, clock), repo, evidence, fines, clock);
    }

    private static DisputeRulingRequestDto Move(string slot = "B-12", string? violator = "51A99999") =>
        new(DisputeDecision.MoveToSlot, "Chỗ A-05 bị xe 51A-999.99 đậu sai, chuyển sang chỗ trống B-12.", NewSlotCode: slot, ViolatorPlate: violator);

    private static DisputeRulingRequestDto Refund(decimal amount, string? violator = null) =>
        new(DisputeDecision.ForceRefund, "Bãi không còn chỗ trống phù hợp, hoàn tiền cho tài xế.", amount, ViolatorPlate: violator);

    private static DisputeRulingRequestDto Reject() =>
        new(DisputeDecision.RejectComplaint, "Camera cho thấy tài xế đến sau giờ giữ chỗ, chỗ đã được giải phóng đúng quy định.");

    // ===== Quy tắc =====

    [Fact]
    public void Business_days_skip_the_weekend()
    {
        Assert.Equal(new DateTime(2026, 11, 11, 3, 0, 0, DateTimeKind.Utc), DisputeRules.AddBusinessDays(Now, 3));   // T6 → T4
        Assert.Equal(new DateTime(2026, 11, 13, 3, 0, 0, DateTimeKind.Utc), DisputeRules.AddBusinessDays(Now, 5));   // T6 → T6 tuần sau
    }

    [Fact]
    public void Ticket_created_on_weekend_in_vietnam_time_counts_from_monday()
    {
        // 18:00 UTC thứ Sáu = 01:00 thứ Bảy giờ Việt Nam → tính từ 00:00 thứ Hai 09/11 → +3 ngày = 00:00 thứ Năm 12/11 (VN).
        var friday18Utc = new DateTime(2026, 11, 6, 18, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 11, 11, 17, 0, 0, DateTimeKind.Utc), DisputeRules.AddBusinessDays(friday18Utc, 3));
    }

    [Theory]
    [InlineData(ComplaintStatus.AwaitingOwner, ComplaintCategory.SlotOccupied, false, true)]
    [InlineData(ComplaintStatus.OwnerResponded, ComplaintCategory.Overcharge, false, true)]
    [InlineData(ComplaintStatus.Escalated, ComplaintCategory.SlotOccupied, false, true)]    // quá 48h vẫn chỉ chủ bãi xử lý
    [InlineData(ComplaintStatus.Open, ComplaintCategory.AppError, false, false)]           // lỗi app không phải tranh chấp với bãi
    [InlineData(ComplaintStatus.AwaitingOwner, ComplaintCategory.SlotOccupied, true, false)] // đã phán quyết
    [InlineData(ComplaintStatus.Resolved, ComplaintCategory.SlotOccupied, false, false)]
    public void Owner_can_rule_only_once_on_open_lot_disputes(ComplaintStatus status, ComplaintCategory category, bool ruled, bool expected)
        => Assert.Equal(expected, DisputeRules.CanOwnerRule(status, category, ruled));

    // ===== AC1: hồ sơ tranh chấp =====

    [Fact]
    public async Task Case_file_shows_booking_history_gate_log_and_hides_internal_notes()
    {
        var ticket = Ticket();
        ticket.Messages.Add(new ComplaintMessage { SenderParty = ComplaintParty.Admin, Message = "Ghi chú nội bộ cũ", IsInternal = true });
        var sut = Create(ticket);
        sut.Evidence.Sessions = [new GateSessionEvidenceDto("PS-1", "Active", "51F-123.45", Now.AddMinutes(-30), null, 30, "Lpr", 0.97, null)];

        var file = await sut.Service.GetCaseFileAsync(Owner, "CP-1");

        Assert.Equal(("A-05", true), (file.Booking!.SlotCode, file.CanRule));
        Assert.Equal("Lpr", Assert.Single(file.GateSessions).CheckInMethod);
        Assert.Equal("System", Assert.Single(file.Timeline).SenderParty);
        Assert.Null(file.Ruling);
    }

    [Fact]
    public async Task Case_file_is_still_returned_when_booking_and_gate_services_are_down()
    {
        var sut = Create(Ticket());
        sut.Evidence.Down = true;

        var file = await sut.Service.GetCaseFileAsync(Owner, "CP-1");

        Assert.Null(file.Booking);
        Assert.Equal(["BookingService", "GateService"], file.UnavailableSources);
    }

    [Fact]
    public async Task Owner_cannot_see_or_rule_disputes_of_another_owners_lot()
    {
        var sut = Create(Ticket("CP-OTHER", owner: OtherOwner, lot: OtherOwnerLot), Ticket("CP-LOT-OTHER", owner: null, lot: OtherOwnerLot));
        await Assert.ThrowsAsync<NotFoundException>(() => sut.Service.GetCaseFileAsync(Owner, "CP-OTHER"));
        await Assert.ThrowsAsync<NotFoundException>(() => sut.Service.RuleAsync(Owner, OwnerUser, "CP-LOT-OTHER", Reject()));
        Assert.Empty(await sut.Service.ListQueueAsync(Owner, null, false));
    }

    [Fact]
    public async Task Owner_sees_ticket_without_stored_owner_when_lot_is_theirs_and_owner_is_filled_on_ruling()
    {
        var ticket = Ticket(owner: null);
        var sut = Create(ticket);

        Assert.Single(await sut.Service.ListQueueAsync(Owner, null, false));
        await sut.Service.RuleAsync(Owner, OwnerUser, "CP-1", Move(violator: null));

        Assert.Equal(Owner, ticket.OwnerProfileId);
    }

    // ===== AC2: chủ bãi phán quyết =====

    [Fact]
    public async Task Owner_moves_driver_to_free_slot_and_fines_the_car_parked_in_the_wrong_slot()
    {
        var ticket = Ticket();
        var sut = Create(ticket);

        var file = await sut.Service.RuleAsync(Owner, OwnerUser, "CP-1", Move());

        Assert.Equal(("Resolved", false, false), (file.Status, file.PayoutHeld, file.CanRule));
        Assert.Equal(("MoveToSlot", "B-12", OwnerUser), (file.Ruling!.Decision, file.Ruling.NewSlotCode, file.Ruling.DecidedByUserId));
        var violation = Assert.Single(file.Violations);
        Assert.Equal(("51A99999", "A-05", 200000m), (violation.PlateNumber, violation.SlotCode, violation.FineAmount));
        Assert.Equal(Now, ticket.OwnerRespondedAtUtc);                          // phán quyết cũng là phản hồi của chủ bãi
        Assert.Contains(file.Timeline, m => m.SenderParty == "LotOwner" && m.Message.Contains("chỗ B-12") && m.Message.Contains("200.000đ"));

        var ruled = Assert.IsType<DisputeRuled>(sut.Repo.Events[0]);
        Assert.Equal(("MoveToSlot", "A-05", "B-12", 10, OwnerUser), (ruled.Decision, ruled.OldSlotCode, ruled.NewSlotCode, ruled.BookingId, ruled.DecidedByUserId));
        Assert.Equal("DisputeRuled", Assert.IsType<AuditRecorded>(sut.Repo.Events[1]).Action);
        var fined = Assert.IsType<ParkingViolationFined>(sut.Repo.Events[2]);
        Assert.Equal(("51A99999", 200000m, Lot), (fined.PlateNumber, fined.FineAmount, fined.ParkingLotId));
    }

    [Fact]
    public async Task Owner_refund_override_is_audited_and_capped_by_amount_paid()
    {
        var ticket = Ticket();
        var sut = Create(ticket);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => sut.Service.RuleAsync(Owner, OwnerUser, "CP-1", Refund(50001)));
        Assert.Contains("50.000đ", ex.Message);
        Assert.Equal(0, sut.Repo.UpdateCount);

        var file = await sut.Service.RuleAsync(Owner, OwnerUser, "CP-1", Refund(50000, violator: "51A-999.99"));
        Assert.Equal(("Resolved", 50000m), (file.Status, ticket.ResolvedRefundAmount!.Value));
        var audit = Assert.IsType<AuditRecorded>(sut.Repo.Events[1]);
        Assert.Equal(("DisputeRefundOverride", OwnerUser), (audit.Action, audit.UserId));
        Assert.Contains("\"PayoutHeld\":true", audit.OldValuesJson);
        Assert.Single(file.Violations);
    }

    [Fact]
    public async Task Owner_can_reject_a_complaint()
    {
        var sut = Create(Ticket());
        Assert.Equal("Rejected", (await sut.Service.RuleAsync(Owner, OwnerUser, "CP-1", Reject())).Status);
        Assert.DoesNotContain(sut.Repo.Events, e => e is ParkingViolationFined);
    }

    [Fact]
    public async Task Each_dispute_gets_only_one_ruling()
    {
        var sut = Create(Ticket());
        await sut.Service.RuleAsync(Owner, OwnerUser, "CP-1", Reject());
        var ex = await Assert.ThrowsAsync<ConflictException>(() => sut.Service.RuleAsync(Owner, OwnerUser, "CP-1", Move()));
        Assert.Contains("1 lần", ex.Message);
    }

    [Fact]
    public async Task Owner_still_rules_after_48_hours_escalation_and_app_errors_are_not_lot_disputes()
    {
        var sut = Create(Ticket("CP-LATE", ComplaintStatus.Escalated), Ticket("CP-APP", ComplaintStatus.Open, ComplaintCategory.AppError));
        Assert.Equal("Resolved", (await sut.Service.RuleAsync(Owner, OwnerUser, "CP-LATE", Move())).Status);
        await Assert.ThrowsAsync<ConflictException>(() => sut.Service.RuleAsync(Owner, OwnerUser, "CP-APP", Reject()));
    }

    [Theory]
    [InlineData(DisputeDecision.ForceRefund, null, null)]          // ép hoàn mà không có số tiền
    [InlineData(DisputeDecision.ForceRefund, 1000, "B-12")]        // ép hoàn kèm chỗ mới
    [InlineData(DisputeDecision.MoveToSlot, null, null)]           // chuyển chỗ mà không có chỗ mới
    [InlineData(DisputeDecision.MoveToSlot, 1000, "B-12")]         // chuyển chỗ kèm tiền
    [InlineData(DisputeDecision.RejectComplaint, 1000, null)]      // bác mà vẫn có tiền
    [InlineData((DisputeDecision)9, null, null)]                   // loại phán quyết không tồn tại
    public async Task Ruling_data_must_match_decision(DisputeDecision decision, int? refund, string? slot)
    {
        var sut = Create(Ticket());
        await Assert.ThrowsAsync<ValidationException>(() => sut.Service.RuleAsync(Owner, OwnerUser, "CP-1",
            new DisputeRulingRequestDto(decision, "Lý do đủ dài để vượt qua kiểm tra độ dài.", refund, slot)));
    }

    [Fact]
    public async Task Move_to_the_same_slot_or_fining_the_drivers_own_car_is_rejected()
    {
        var sut = Create(Ticket());
        await Assert.ThrowsAsync<ValidationException>(() => sut.Service.RuleAsync(Owner, OwnerUser, "CP-1", Move(slot: "a-05")));
        await Assert.ThrowsAsync<ValidationException>(() => sut.Service.RuleAsync(Owner, OwnerUser, "CP-1", Move(violator: "51F-123.45")));
        await Assert.ThrowsAsync<ValidationException>(() => sut.Service.RuleAsync(Owner, OwnerUser, "CP-1", Move(violator: "ABC")));
    }

    [Fact]
    public async Task Move_and_fine_only_apply_to_slot_occupied_complaints()
    {
        var sut = Create(Ticket(category: ComplaintCategory.Overcharge));
        await Assert.ThrowsAsync<ValidationException>(() => sut.Service.RuleAsync(Owner, OwnerUser, "CP-1", Move()));
        await Assert.ThrowsAsync<ValidationException>(() => sut.Service.RuleAsync(Owner, OwnerUser, "CP-1", Refund(1000, violator: "51A99999")));
        Assert.Equal("Resolved", (await sut.Service.RuleAsync(Owner, OwnerUser, "CP-1", Refund(1000))).Status);
    }

    [Fact]
    public async Task Fine_needs_the_lot_fine_rate_configured_by_owner()
    {
        var sut = Create(Ticket());
        sut.Fines.Rates.Clear();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => sut.Service.RuleAsync(Owner, OwnerUser, "CP-1", Move()));
        Assert.Contains("mức phạt", ex.Message);
        Assert.Equal(0, sut.Repo.UpdateCount);
    }

    [Fact]
    public async Task Precedents_only_include_rulings_of_the_owners_lots()
    {
        var sut = Create(Ticket("CP-A"), Ticket("CP-B", category: ComplaintCategory.Overcharge));
        await sut.Service.RuleAsync(Owner, OwnerUser, "CP-A", Move(violator: null));
        await sut.Service.RuleAsync(Owner, OwnerUser, "CP-B", Reject());

        var p = Assert.Single(await sut.Service.ListPrecedentsAsync(Owner, ComplaintCategory.SlotOccupied));
        Assert.Equal(("CP-A", "MoveToSlot"), (p.ComplaintCode, p.Decision));
        Assert.Empty(await sut.Service.ListPrecedentsAsync(OtherOwner, null));
    }

    // ===== AC3: SLA =====

    [Fact]
    public async Task Owner_message_counts_as_first_response()
    {
        var sut = Create(Ticket());
        var file = await sut.Service.AddMessageAsync(Owner, OwnerUser, "CP-1", new DisputeMessageRequestDto("Nhân viên đang kiểm tra chỗ A-05."));
        Assert.Equal((Now, false), (file.Sla.FirstRespondedAtUtc!.Value, file.Sla.FirstResponseBreached));
        Assert.Equal("LotOwner", file.Timeline[^1].SenderParty);
    }

    [Fact]
    public async Task Queue_puts_sla_breaches_first_and_can_filter_them()
    {
        var late = Ticket("CP-LATE", createdAt: Now.AddHours(-25));      // quá 24 giờ chưa phản hồi
        var fresh = Ticket("CP-NEW", createdAt: Now.AddHours(-1));
        var sut = Create(fresh, late, Ticket("CP-CLOSED", ComplaintStatus.Resolved));

        var queue = await sut.Service.ListQueueAsync(Owner, null, false);
        Assert.Equal(["CP-LATE", "CP-NEW"], queue.Select(q => q.Code));
        Assert.True(queue[0].Sla.FirstResponseBreached);

        Assert.Equal("CP-LATE", Assert.Single(await sut.Service.ListQueueAsync(Owner, null, slaBreachedOnly: true)).Code);
        await Assert.ThrowsAsync<ValidationException>(() => sut.Service.ListQueueAsync(Owner, ComplaintStatus.Resolved, false));
    }

    [Fact]
    public async Task Resolution_after_five_business_days_is_a_breach()
    {
        var sut = Create(Ticket(createdAt: Now));
        sut.Clock.UtcNow = DisputeRules.AddBusinessDays(Now, 5).AddMinutes(1);

        Assert.True((await sut.Service.RuleAsync(Owner, OwnerUser, "CP-1", Reject())).Sla.ResolutionBreached);
    }

    [Fact]
    public void Domain_rejects_second_ruling_and_mismatched_data()
    {
        var ticket = Ticket();
        Assert.Throws<ArgumentException>(() =>
            Ticket(category: ComplaintCategory.Overcharge).ApplyOwnerRuling(OwnerUser, DisputeDecision.MoveToSlot, null, "B-1", "Lý do", Now));
        ticket.ApplyOwnerRuling(OwnerUser, DisputeDecision.RejectComplaint, null, null, "Lý do", Now);
        Assert.Throws<InvalidOperationException>(() =>
            ticket.ApplyOwnerRuling(OwnerUser, DisputeDecision.ForceRefund, 1000, null, "Lý do", Now));
    }

    // ===== Mức phạt đậu sai chỗ =====

    [Fact]
    public async Task Owner_sets_fine_rate_only_for_own_lot_within_limits()
    {
        var service = new LotFineService(new FakeFines(), new FakeOwnerLots());

        var rate = await service.SetRateAsync(Owner, OwnerUser, Lot, new LotFineRateRequestDto(150000));
        Assert.Equal((Lot, Owner, 150000m), (rate.ParkingLotId, rate.OwnerProfileId, rate.WrongSlotFine));
        Assert.Equal(150000m, (await service.GetRateAsync(Owner, Lot)).WrongSlotFine);

        await Assert.ThrowsAsync<NotFoundException>(() => service.SetRateAsync(Owner, OwnerUser, OtherOwnerLot, new LotFineRateRequestDto(1000)));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetRateAsync(OtherOwner, Lot));
        await Assert.ThrowsAsync<ValidationException>(() => service.SetRateAsync(Owner, OwnerUser, Lot, new LotFineRateRequestDto(0)));
        await Assert.ThrowsAsync<ValidationException>(() => service.SetRateAsync(Owner, OwnerUser, Lot, new LotFineRateRequestDto(5_000_001)));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ListViolationsAsync(Owner, OtherOwnerLot));
    }

    // ===== Hỗ trợ =====

    private static void SetCreatedAt(Complaint complaint, DateTime createdAtUtc)
        => typeof(ParkingManagement.SharedKernel.Domain.BaseEntity).GetProperty(nameof(Complaint.CreatedAtUtc))!.SetValue(complaint, createdAtUtc);

    private sealed class MutableClock(DateTime utcNow) : TimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => new(UtcNow);
    }

    private sealed class FakeOwnerLots : IOwnerLotLookup
    {
        public Task<IReadOnlyCollection<int>> GetLotIdsAsync(int ownerProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<int>>(ownerProfileId switch { Owner => [Lot], OtherOwner => [OtherOwnerLot], _ => [] });
    }

    private sealed class FakeFines : ILotFineRepository
    {
        public Dictionary<int, LotFineRate> Rates { get; } = [];

        public Task<LotFineRate?> FindRateAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult(Rates.GetValueOrDefault(parkingLotId));

        public Task SaveRateAsync(LotFineRate rate, CancellationToken cancellationToken = default)
        {
            Rates[rate.ParkingLotId] = rate;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ParkingViolation>> ListViolationsAsync(IReadOnlyCollection<int> parkingLotIds, int take,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ParkingViolation>>([]);
    }

    private sealed class FakeEvidence : IBookingHistoryLookup, IGateSessionLookup
    {
        public BookingEvidenceDto? Booking { get; set; }
        public IReadOnlyList<GateSessionEvidenceDto> Sessions { get; set; } = [];
        public bool Down { get; set; }

        public Task<BookingEvidenceDto?> GetAsync(string bookingCode, CancellationToken cancellationToken = default)
            => Down ? throw new DependencyUnavailableException("BookingService tắt") : Task.FromResult(Booking);

        public Task<IReadOnlyList<GateSessionEvidenceDto>> ListForBookingAsync(int parkingLotId, string bookingCode,
            CancellationToken cancellationToken = default)
            => Down ? throw new DependencyUnavailableException("GateService tắt") : Task.FromResult(Sessions);
    }
}
