using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Commission;
using PaymentService.Application.Features.Settlements;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Services;
using static PaymentService.Test.OwnerRevenueTestData;
using static PaymentService.Test.SettlementTestData;

namespace PaymentService.Test;

// US-080: sinh kỳ quyết toán (job, Admin sinh kỳ, Admin tính lại) trên pm_payment trong bộ nhớ chạy đúng query shapes.
public class SettlementGenerationTests
{
    private const int Owner = OwnerA;
    private const int Lot = LotA1;
    private const int AdminId = 7;
    private static readonly DateTime Now = Utc(2026, 11, 10, 3);                // Thứ Ba 10/11 ICT
    private static readonly DateOnly Week45 = new(2026, 11, 2);                 // 02/11 – 08/11
    private static readonly DateTime Cutoff45 = Utc(2026, 11, 8, 17);

    private sealed class Harness
    {
        public InMemorySettlementRepository Repo { get; } = new();
        public FakeCycleClient Cycles { get; set; } = new();
        public FakeBookingClient Bookings { get; } = new();
        public FakeHoldClient Holds { get; } = new();
        public DateTime Now { get; set; } = SettlementGenerationTests.Now;
        public CommissionPolicy Policy { get; set; } = new([]);

        private SettlementBuilder Builder => new(Repo, Cycles, Bookings, Policy, new SettlementOptions(SettlementOptions.DefaultBiweeklyAnchor));
        public RunSettlementGenerationUseCase Job => new(Repo, Builder, new FixedTimeProvider(Now), NullLogger<RunSettlementGenerationUseCase>.Instance);
        public GenerateSettlementsUseCase Generate => new(Repo, Builder, Holds, new FixedTimeProvider(Now));
        public RegenerateSettlementUseCase Regenerate => new(Repo, Builder, Holds, new FixedTimeProvider(Now));

        public Payment Pay(int id, decimal amount, DateTime paidAtUtc, PaymentMethod method = PaymentMethod.Vnpay,
            PaymentPurpose purpose = PaymentPurpose.Booking, string? bookingCode = "auto", decimal? rate = 0.10m, int owner = Owner)
        {
            var payment = Paid(owner, Lot, amount, paidAtUtc, method, purpose).WithId(id);
            payment.BookingCode = bookingCode == "auto" ? (purpose is PaymentPurpose.Booking or PaymentPurpose.Extension ? $"BK-{id}" : null) : bookingCode;
            Repo.Payments.Add(payment);
            if (rate is { } r) Repo.Entries.Add(Accrual(payment, r));
            return payment;
        }

        public Settlement Single(int owner = Owner) => Assert.Single(Repo.Settlements, s => s.OwnerProfileId == owner);
    }

    // ===================== Lịch kỳ =====================

    [Fact]
    public async Task FirstRun_CreatesLastEndedWeek_ResolvingCycleAtStartOfYesterday()
    {
        var h = new Harness();

        var result = await h.Job.ExecuteAsync();

        var period = Assert.Single(h.Repo.Periods);
        Assert.Equal(Week45, period.PeriodStart);
        Assert.Equal(new DateOnly(2026, 11, 8), period.PeriodEnd);
        Assert.Equal("WEEKLY", period.Cycle);
        Assert.Equal(Utc(2026, 11, 8, 17), h.Cycles.Calls[0]);                  // 00:00 ICT 09/11 – chu kỳ của khối chứa hôm qua
        Assert.Equal(2, h.Cycles.Calls.Count);                                  // hỏi tiếp kỳ 09/11 mới biết nó chưa kết thúc → không tạo
        Assert.Same(period, result.Period);
        Assert.Equal(0, result.Created);
    }

    [Fact]
    public async Task CatchUp_AfterCycleChange_CreatesPartialMonthAndStopsBeforeUnendedPeriod()
    {
        var h = new Harness
        {
            Cycles = new FakeCycleClient(at => at >= Utc(2026, 10, 25, 17)
                ? new SettlementCycleQuote("MONTHLY", Utc(2026, 10, 25, 17))
                : new SettlementCycleQuote("WEEKLY", null)),
        };
        h.Repo.Periods.Add(SettlementPeriod.Create(new DateOnly(2026, 10, 19), SettlementCycle.Weekly, null, SettlementOptions.DefaultBiweeklyAnchor, Now)
            .WithId(1));

        var result = await h.Job.ExecuteAsync();

        Assert.Equal(2, h.Repo.Periods.Count);
        Assert.Equal(new DateOnly(2026, 10, 26), result.Period!.PeriodStart);
        Assert.Equal(new DateOnly(2026, 10, 31), result.Period.PeriodEnd);     // kỳ tháng đầu chỉ tới cuối tháng
        Assert.Equal("MONTHLY", result.Period.Cycle);
        Assert.Equal(Utc(2026, 10, 25, 17), result.Period.CycleEffectiveFromUtc);
    }

    [Fact]
    public async Task AdminServiceDown_NoPeriodAndNoSettlement()
    {
        var h = new Harness();
        h.Cycles.Fail = true;
        h.Pay(1, 100_000, Utc(2026, 11, 3, 2), PaymentMethod.Cash, PaymentPurpose.WalkIn);

        await Assert.ThrowsAsync<DependencyUnavailableException>(() => h.Job.ExecuteAsync());

        Assert.Empty(h.Repo.Periods);
        Assert.Empty(h.Repo.Settlements);
    }

    // ===================== Job =====================

    [Fact]
    public async Task Job_IncludesOnlyPaymentsCompletedBeforeCutoff_AndBlocksHoldsNotLoaded()
    {
        var h = new Harness();
        var p1 = h.Pay(1, 100_000, Utc(2026, 11, 3, 2));                                         // booking xong 05/11
        h.Pay(2, 50_000, Utc(2026, 11, 4, 2), PaymentMethod.Cash, PaymentPurpose.WalkIn);         // thu tại cổng
        h.Pay(3, 80_000, Utc(2026, 11, 4, 2));                                                   // booking còn Confirmed
        h.Pay(4, 70_000, Utc(2026, 11, 6, 2));                                                   // kết thúc sau mốc chốt
        h.Pay(5, 60_000, Utc(2026, 11, 9, 2));                                                   // thu sau mốc chốt
        h.Pay(6, 40_000, Utc(2026, 11, 3, 2), PaymentMethod.Cash, PaymentPurpose.WalkIn, owner: OwnerB);
        h.Bookings.Statuses["BK-1"] = Ended("Completed", Utc(2026, 11, 5, 4));
        h.Bookings.Statuses["BK-3"] = new BookingStatusInfo(true, "Confirmed", Utc(2026, 11, 2, 1));
        h.Bookings.Statuses["BK-4"] = Ended("Completed", Utc(2026, 11, 9, 1));

        var result = await h.Job.ExecuteAsync();

        Assert.Equal(2, result.Created);
        var s = h.Single();
        Assert.Equal("STL-20261102-0001", s.Code);
        Assert.Equal(SettlementStatus.Draft, s.Status);
        Assert.Equal([1, 2], s.Lines.Select(l => l.PaymentId!.Value).Order());
        Assert.Equal(150_000, s.GrossAmount);
        Assert.Equal(50_000, s.OwnerCollectedAmount);
        Assert.Equal(15_000, s.CommissionAmount);
        Assert.Equal(85_000, s.NetPayout);
        Assert.True(s.IsBlocked);
        Assert.Equal(SettlementBlockReasons.HoldsNotLoaded, s.BlockedReasons);
        Assert.Null(s.HoldsCapturedAtUtc);
        Assert.Null(s.GeneratedByUserId);
        Assert.Equal(Utc(2026, 11, 5, 4), s.Lines.Single(l => l.PaymentId == p1.Id).OccurredAtUtc);   // ghi nhận lúc booking kết thúc
        Assert.Equal(["BK-1", "BK-3", "BK-4"], Assert.Single(h.Bookings.Calls).Order());              // BK-5 sau mốc chốt không cần hỏi
        Assert.Equal(40_000, h.Single(OwnerB).OwnerCollectedAmount);                                     // chủ bãi khác: kỳ riêng

        var audit = h.Repo.Outbox.Select(o => JsonSerializer.Deserialize<JsonElement>(o.PayloadJson)).First(p => p.GetProperty("entityId").GetString() == s.Code);
        Assert.Equal("Settlement.Generated", audit.GetProperty("action").GetString());
        Assert.Equal("PaymentService", audit.GetProperty("sourceService").GetString());
        Assert.All(h.Repo.Outbox, o => Assert.Equal(nameof(AuditRecorded), o.EventType));
    }

    [Fact]
    public async Task Job_RunTwice_IsIdempotent()
    {
        var h = new Harness();
        h.Pay(2, 50_000, Utc(2026, 11, 4, 2), PaymentMethod.Cash, PaymentPurpose.WalkIn);
        await h.Job.ExecuteAsync();
        var lines = h.Single().Lines.Count;

        var second = await h.Job.ExecuteAsync();

        Assert.Equal(0, second.Created);
        Assert.Equal(0, second.SkippedOwners);                                  // không còn gì chưa quyết toán → không còn ứng viên
        Assert.Equal(lines, h.Single().Lines.Count);
        Assert.Single(h.Repo.Outbox);
    }

    [Fact]
    public async Task Job_DoesNotOverwriteExistingDraft()
    {
        var h = new Harness();
        h.Pay(2, 50_000, Utc(2026, 11, 4, 2), PaymentMethod.Vnpay, PaymentPurpose.WalkIn);
        await h.Generate.ExecuteAsync(new GenerateSettlementsCommand(null, null), AdminId);       // Admin đã nạp khoản giữ
        h.Repo.Adjustments.Add(new FinancialAdjustment
        {
            OwnerProfileId = Owner, Amount = 5_000, Reason = "Bù", CreatedByUserId = 8, Status = FinancialAdjustmentStatus.Approved,
            ApprovedByUserId = AdminId, ApprovedAtUtc = Utc(2026, 11, 10, 1),
        }.WithId(1));

        var run = await h.Job.ExecuteAsync();

        Assert.Equal(1, run.SkippedOwners);                                     // AlreadyGenerated
        Assert.Equal(Now, h.Single().HoldsCapturedAtUtc);
        Assert.False(h.Single().IsBlocked);
        Assert.Null(h.Repo.Adjustments[0].SettlementId);                        // gắn khi Admin tính lại
    }

    [Fact]
    public async Task LockHeldByAnotherInstance_SkipsRun()
    {
        var h = new Harness();
        h.Repo.LockAvailable = false;
        h.Pay(2, 50_000, Utc(2026, 11, 4, 2), PaymentMethod.Cash, PaymentPurpose.WalkIn);

        var result = await h.Job.ExecuteAsync();

        Assert.True(result.Skipped);
        Assert.Empty(h.Repo.Periods);
        Assert.Empty(h.Cycles.Calls);
    }

    [Fact]
    public async Task BookingServiceDown_NothingCreated()
    {
        var h = new Harness();
        h.Pay(1, 100_000, Utc(2026, 11, 3, 2));
        h.Bookings.Fail = true;

        await Assert.ThrowsAsync<DependencyUnavailableException>(() => h.Job.ExecuteAsync());
        Assert.Empty(h.Repo.Settlements);
    }

    [Fact]
    public async Task BookingNotFoundOrCodeMissing_IncludedButBlocked()
    {
        var h = new Harness();
        h.Pay(1, 100_000, Utc(2026, 11, 3, 2));                                              // BookingService: 404
        h.Pay(2, 30_000, Utc(2026, 11, 3, 2), bookingCode: null);                            // không có mã booking

        await h.Job.ExecuteAsync();

        var s = h.Single();
        Assert.Equal(SettlementBlockReasons.BookingNotFound, s.Lines.Single(l => l.PaymentId == 1).BlockedReason);
        Assert.Equal(SettlementBlockReasons.BookingCodeMissing, s.Lines.Single(l => l.PaymentId == 2).BlockedReason);
        Assert.Contains(SettlementBlockReasons.BookingNotFound, s.BlockedReasons);
        Assert.Contains(SettlementBlockReasons.BookingCodeMissing, s.BlockedReasons);
    }

    [Fact]
    public async Task CommissionNotYetAccrued_BlocksCommissionPending()
    {
        var h = new Harness();
        h.Pay(2, 50_000, Utc(2026, 11, 4, 2), PaymentMethod.Cash, PaymentPurpose.WalkIn, rate: null);

        await h.Job.ExecuteAsync();

        Assert.Contains(SettlementBlockReasons.CommissionPending, h.Single().BlockedReasons);
        Assert.Equal(0, h.Single().CommissionAmount);
    }

    [Fact]
    public async Task NextPeriod_WaitsForEarlierDraft_ThenPicksUpBookingsCompletedLater()
    {
        var h = new Harness();
        h.Pay(1, 100_000, Utc(2026, 11, 3, 2));
        h.Pay(3, 80_000, Utc(2026, 11, 4, 2));
        h.Bookings.Statuses["BK-1"] = Ended("Completed", Utc(2026, 11, 5, 4));
        h.Bookings.Statuses["BK-3"] = new BookingStatusInfo(true, "Parking", Utc(2026, 11, 8, 1));
        await h.Job.ExecuteAsync();

        h.Now = Utc(2026, 11, 17, 3);
        h.Bookings.Statuses["BK-3"] = Ended("NoShow", Utc(2026, 11, 10, 2));             // No-show: chủ bãi vẫn nhận tiền trừ hoa hồng
        var waiting = await h.Job.ExecuteAsync();
        Assert.Equal(0, waiting.Created);                                                // kỳ 45 còn Draft → chưa sinh kỳ 46

        h.Single().Status = SettlementStatus.Approved;
        var next = await h.Job.ExecuteAsync();

        Assert.Equal(1, next.Created);
        var s46 = h.Repo.Settlements.Single(s => s.PeriodStart == new DateOnly(2026, 11, 9));
        Assert.Equal(3, Assert.Single(s46.Lines).PaymentId);
        Assert.Equal(72_000, s46.NetPayout);
    }

    [Fact]
    public async Task LateRefund_GoesToNextPeriod_AndNegativeNetCarriesOver()
    {
        var h = new Harness();
        var p1 = h.Pay(2, 100_000, Utc(2026, 11, 4, 2), PaymentMethod.Vnpay, PaymentPurpose.Overstay);
        await h.Job.ExecuteAsync();
        var s45 = h.Single();
        s45.Status = SettlementStatus.Paid;
        Assert.Equal(90_000, s45.NetPayout);

        p1.RefundedAmount = 100_000;
        h.Repo.Refunds.Add(CompletedRefund(50, p1, 100_000, Utc(2026, 11, 10, 2)));      // hoàn sau khi kỳ 45 đã chi
        h.Now = Utc(2026, 11, 17, 3);
        await h.Job.ExecuteAsync();
        var s46 = h.Repo.Settlements.Single(s => s.PeriodStart == new DateOnly(2026, 11, 9));
        var refund = Assert.Single(s46.Lines);
        Assert.Equal(SettlementLineType.Refund, refund.LineType);
        Assert.Equal(50, refund.RefundId);
        Assert.Equal(-10_000, refund.CommissionAmount);
        Assert.Equal(-90_000, s46.NetPayout);
        s46.Status = SettlementStatus.Approved;

        h.Pay(3, 200_000, Utc(2026, 11, 18, 2), PaymentMethod.Vnpay, PaymentPurpose.Overstay);
        h.Now = Utc(2026, 11, 24, 3);
        await h.Job.ExecuteAsync();
        var s47 = h.Repo.Settlements.Single(s => s.PeriodStart == new DateOnly(2026, 11, 16));
        Assert.Equal(-90_000, s47.CarryOverAmount);
        Assert.Equal(s46.Id, s47.CarryOverFromSettlementId);
        Assert.Equal(200_000 - 20_000 - 90_000, s47.NetPayout);
    }

    [Fact]
    public async Task Voucher_ChargedOnce_EvenAfterRegenerate()
    {
        var h = new Harness();
        h.Repo.Vouchers.Add(new CompensationVoucher
        {
            Code = "COMP-0001", UserId = 99, Amount = 50_000, Reason = "Bãi hết chỗ", ChargedToOwnerProfileId = Owner, IssuedByUserId = AdminId,
            ExpiresAtUtc = Utc(2026, 12, 1), Status = VoucherStatus.Issued,
        }.CreatedAtUtc(Utc(2026, 11, 3, 2)).WithId(1));

        await h.Job.ExecuteAsync();
        var s = h.Single();
        Assert.Equal(-50_000, s.AdjustmentAmount);
        var charge = Assert.Single(h.Repo.Adjustments);
        Assert.Equal(FinancialAdjustmentSource.CompensationVoucher, charge.SourceType);
        Assert.Equal(FinancialAdjustmentStatus.Approved, charge.Status);
        Assert.Equal("COMP-0001", charge.SourceReference);
        Assert.Equal(s.Id, charge.SettlementId);

        await h.Generate.ExecuteAsync(new GenerateSettlementsCommand(null, null), AdminId);

        Assert.Single(h.Repo.Adjustments);
        Assert.Equal(-50_000, h.Single().AdjustmentAmount);
        Assert.Equal(s.Id, charge.SettlementId);
    }

    [Fact]
    public async Task ApprovedAdjustments_AttachedAtGeneration_PendingOnesAreNot()
    {
        var h = new Harness();
        h.Pay(2, 50_000, Utc(2026, 11, 4, 2), PaymentMethod.Vnpay, PaymentPurpose.WalkIn);
        h.Repo.Adjustments.Add(new FinancialAdjustment
        {
            OwnerProfileId = Owner, Amount = 20_000, Reason = "Sửa hoa hồng", CreatedByUserId = 8, Status = FinancialAdjustmentStatus.Approved,
            ApprovedByUserId = AdminId, ApprovedAtUtc = Utc(2026, 11, 9, 2),
        }.WithId(1));
        h.Repo.Adjustments.Add(new FinancialAdjustment
        {
            OwnerProfileId = Owner, Amount = -99_000, Reason = "Chờ duyệt", CreatedByUserId = 8, Status = FinancialAdjustmentStatus.PendingApproval,
        }.WithId(2));

        await h.Job.ExecuteAsync();

        var s = h.Single();
        Assert.Equal(20_000, s.AdjustmentAmount);
        Assert.Equal(s.Id, h.Repo.Adjustments.Single(a => a.Id == 1).SettlementId);
        Assert.Null(h.Repo.Adjustments.Single(a => a.Id == 2).SettlementId);
        Assert.Equal(45_000 + 20_000, s.NetPayout);
    }

    // ===================== Admin sinh kỳ =====================

    [Fact]
    public async Task AdminGenerate_LoadsHolds_NotBlocked()
    {
        var h = new Harness();
        h.Pay(2, 100_000, Utc(2026, 11, 4, 2), PaymentMethod.Momo, PaymentPurpose.WalkIn);
        h.Holds.Holds.Add(new PayoutHoldItem("CP-0001", "AwaitingOwner", Lot, Owner, "BK-77", 30_000, Utc(2026, 11, 5)));
        h.Holds.Holds.Add(new PayoutHoldItem("CP-0002", "Open", 21, OwnerB, null, 10_000, Utc(2026, 11, 5)));   // chủ bãi khác, không phát sinh

        var result = await h.Generate.ExecuteAsync(new GenerateSettlementsCommand(null, null), AdminId);

        Assert.Equal(Week45, result.PeriodStart);
        Assert.Equal("WEEKLY", result.Cycle);
        var outcome = Assert.Single(result.Owners);                                       // chủ bãi B chỉ có khoản giữ → không sinh kỳ
        Assert.Equal(SettlementOutcomes.Created, outcome.Outcome);
        Assert.False(outcome.IsBlocked);
        Assert.NotNull(outcome.RowVersion);
        var s = h.Single();
        Assert.Equal(30_000, s.HeldAmount);
        Assert.Equal(100_000 - 10_000 - 30_000, s.NetPayout);
        Assert.Equal(AdminId, s.GeneratedByUserId);
        Assert.Equal(Now, s.HoldsCapturedAtUtc);
        Assert.Equal("CP-0001", s.Lines.Single(l => l.LineType == SettlementLineType.DisputeHold).ReferenceCode);
        Assert.Equal([null], h.Holds.Calls);
    }

    [Fact]
    public async Task AdminGenerate_RegeneratesJobDraft_RemovingHoldsNotLoaded()
    {
        var h = new Harness();
        h.Pay(2, 100_000, Utc(2026, 11, 4, 2), PaymentMethod.Momo, PaymentPurpose.WalkIn);
        await h.Job.ExecuteAsync();
        var draft = h.Single();
        var oldVersion = draft.RowVersion;

        var result = await h.Generate.ExecuteAsync(new GenerateSettlementsCommand(Week45, Owner), AdminId);

        var outcome = Assert.Single(result.Owners);
        Assert.Equal(SettlementOutcomes.Regenerated, outcome.Outcome);
        Assert.Equal(draft.Id, outcome.SettlementId);
        Assert.False(draft.IsBlocked);
        Assert.Null(draft.BlockedReasons);
        Assert.NotEqual(oldVersion, draft.RowVersion);
        Assert.Single(draft.Lines);
        Assert.Equal([Owner], h.Holds.Calls.Select(c => c!.Value));
        Assert.Contains(h.Repo.Outbox, o => o.PayloadJson.Contains("Settlement.Regenerated"));
    }

    [Fact]
    public async Task AdminGenerate_ReleasesHoldClosedSinceEarlierPeriod()
    {
        var h = new Harness();
        h.Pay(2, 100_000, Utc(2026, 11, 4, 2), PaymentMethod.Momo, PaymentPurpose.WalkIn);
        h.Holds.Holds.Add(new PayoutHoldItem("CP-0001", "Escalated", Lot, Owner, null, 30_000, Utc(2026, 11, 5)));
        await h.Generate.ExecuteAsync(new GenerateSettlementsCommand(null, null), AdminId);
        h.Single().Status = SettlementStatus.Paid;

        h.Holds.Holds.Clear();                                                            // khiếu nại đã có phán quyết
        h.Now = Utc(2026, 11, 17, 3);
        var result = await h.Generate.ExecuteAsync(new GenerateSettlementsCommand(null, null), AdminId);

        Assert.Equal(SettlementOutcomes.Created, Assert.Single(result.Owners).Outcome);
        var s46 = h.Repo.Settlements.Single(s => s.PeriodStart == new DateOnly(2026, 11, 9));
        var release = Assert.Single(s46.Lines);
        Assert.Equal(30_000, release.Amount);
        Assert.Equal(-30_000, s46.HeldAmount);
        Assert.Equal(30_000, s46.NetPayout);
    }

    [Fact]
    public async Task AdminGenerate_Validation()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<AuthenticationException>(() => h.Generate.ExecuteAsync(new GenerateSettlementsCommand(null, null), null));
        await Assert.ThrowsAsync<ValidationException>(() => h.Generate.ExecuteAsync(new GenerateSettlementsCommand(null, 0), AdminId));
        await Assert.ThrowsAsync<ValidationException>(() =>
            h.Generate.ExecuteAsync(new GenerateSettlementsCommand(new DateOnly(2026, 11, 3), null), AdminId));       // không phải đầu kỳ
        await Assert.ThrowsAsync<ValidationException>(() =>
            h.Generate.ExecuteAsync(new GenerateSettlementsCommand(new DateOnly(2026, 11, 9), null), AdminId));       // kỳ chưa kết thúc
    }

    [Fact]
    public async Task AdminGenerate_LockHeld_Conflict()
    {
        var h = new Harness();
        h.Repo.LockAvailable = false;
        await Assert.ThrowsAsync<ConflictException>(() => h.Generate.ExecuteAsync(new GenerateSettlementsCommand(null, null), AdminId));
    }

    [Fact]
    public async Task AdminGenerate_SupportServiceDown_NothingCreated()
    {
        var h = new Harness();
        h.Pay(2, 100_000, Utc(2026, 11, 4, 2), PaymentMethod.Momo, PaymentPurpose.WalkIn);
        h.Holds.Fail = true;

        await Assert.ThrowsAsync<DependencyUnavailableException>(() => h.Generate.ExecuteAsync(new GenerateSettlementsCommand(null, null), AdminId));
        Assert.Empty(h.Repo.Settlements);
    }

    [Fact]
    public async Task AdminGenerate_NonDraftOrOpenEarlier_Skipped()
    {
        var h = new Harness();
        h.Pay(2, 100_000, Utc(2026, 11, 4, 2), PaymentMethod.Momo, PaymentPurpose.WalkIn);
        await h.Generate.ExecuteAsync(new GenerateSettlementsCommand(null, null), AdminId);
        h.Single().Status = SettlementStatus.PendingReview;

        var again = await h.Generate.ExecuteAsync(new GenerateSettlementsCommand(Week45, Owner), AdminId);
        Assert.Equal(SettlementOutcomes.NotDraft, Assert.Single(again.Owners).Reason);

        h.Pay(3, 100_000, Utc(2026, 11, 10, 2), PaymentMethod.Momo, PaymentPurpose.WalkIn);
        h.Now = Utc(2026, 11, 17, 3);
        var next = await h.Generate.ExecuteAsync(new GenerateSettlementsCommand(null, Owner), AdminId);
        Assert.Equal(SettlementOutcomes.OpenEarlierSettlement, Assert.Single(next.Owners).Reason);
    }

    // ===================== Admin tính lại =====================

    [Fact]
    public async Task Regenerate_AfterCommissionAccrued_Unblocks()
    {
        var h = new Harness();
        var payment = h.Pay(2, 50_000, Utc(2026, 11, 4, 2), PaymentMethod.Vnpay, PaymentPurpose.WalkIn, rate: null);
        await h.Job.ExecuteAsync();
        var draft = h.Single();
        Assert.Contains(SettlementBlockReasons.CommissionPending, draft.BlockedReasons);

        h.Repo.Entries.Add(Accrual(payment, 0.10m));                                     // job US-079 vừa tính
        var outcome = await h.Regenerate.ExecuteAsync(draft.Id, Convert.ToBase64String(draft.RowVersion), AdminId);

        Assert.Equal(SettlementOutcomes.Regenerated, outcome.Outcome);
        Assert.False(draft.IsBlocked);
        Assert.Equal(5_000, draft.CommissionAmount);
        Assert.Equal(45_000, draft.NetPayout);
        Assert.Equal(Convert.ToBase64String(draft.RowVersion), outcome.RowVersion);
    }

    [Fact]
    public async Task Regenerate_StaleRowVersion_Conflict()
    {
        var h = new Harness();
        h.Pay(2, 50_000, Utc(2026, 11, 4, 2), PaymentMethod.Vnpay, PaymentPurpose.WalkIn);
        await h.Job.ExecuteAsync();

        await Assert.ThrowsAsync<ConflictException>(() =>
            h.Regenerate.ExecuteAsync(h.Single().Id, Convert.ToBase64String(Guid.NewGuid().ToByteArray()), AdminId));
    }

    [Fact]
    public async Task Regenerate_NotDraft_Conflict()
    {
        var h = new Harness();
        h.Pay(2, 50_000, Utc(2026, 11, 4, 2), PaymentMethod.Vnpay, PaymentPurpose.WalkIn);
        await h.Job.ExecuteAsync();
        var s = h.Single();
        s.Status = SettlementStatus.Approved;
        var before = s.NetPayout;

        await Assert.ThrowsAsync<ConflictException>(() => h.Regenerate.ExecuteAsync(s.Id, Convert.ToBase64String(s.RowVersion), AdminId));
        Assert.Equal(before, s.NetPayout);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64!")]
    public async Task Regenerate_MissingOrInvalidRowVersion_Validation(string? rowVersion)
        => await Assert.ThrowsAsync<ValidationException>(() => new Harness().Regenerate.ExecuteAsync(1, rowVersion, AdminId));

    [Fact]
    public async Task Regenerate_UnknownSettlement_NotFound()
        => await Assert.ThrowsAsync<NotFoundException>(() => new Harness().Regenerate.ExecuteAsync(99, "AAAA", AdminId));

    // ===================== Xác minh booking (hàm thuần) =====================

    [Theory]
    [InlineData("Completed", true)]
    [InlineData("NoShow", true)]
    [InlineData("CancelledNoRefund", true)]
    [InlineData("Cancelled", true)]
    [InlineData("Expired", true)]
    [InlineData("Rejected", true)]
    [InlineData("Confirmed", false)]
    [InlineData("CheckedIn", false)]
    [InlineData("Parking", false)]
    [InlineData("CheckedOut", false)]
    public void Recognize_OnlyTerminalBookings(string status, bool included)
    {
        var row = new UnsettledPaymentRow(1, "PM-1", "BK-1", Lot, PaymentPurpose.Booking, PaymentMethod.Vnpay, 100_000, Utc(2026, 11, 3));
        var statuses = new Dictionary<string, BookingStatusInfo> { ["bk-1"] = Ended(status, Utc(2026, 11, 5)) };

        var input = SettlementBuilder.Recognize(row, statuses, Cutoff45);

        Assert.Equal(included, input is not null);
        if (included) Assert.Equal(Utc(2026, 11, 5), input!.RecognizedAtUtc);
    }

    [Fact]
    public void Recognize_TerminalWithoutHistory_BlockedUnverifiable()
    {
        var row = new UnsettledPaymentRow(1, "PM-1", "BK-1", Lot, PaymentPurpose.Extension, PaymentMethod.Vnpay, 100_000, Utc(2026, 11, 3));
        var input = SettlementBuilder.Recognize(row, new Dictionary<string, BookingStatusInfo> { ["BK-1"] = new(true, "Completed", null) }, Cutoff45);
        Assert.Equal(SettlementBlockReasons.BookingUnverifiable, input!.BlockedReason);
    }

    [Theory]
    [InlineData(PaymentPurpose.WalkIn)]
    [InlineData(PaymentPurpose.Overstay)]
    [InlineData(PaymentPurpose.MonthlyPass)]
    public void Recognize_GateAndMonthlyPass_AtPaidTime_NoBookingCheck(PaymentPurpose purpose)
    {
        var row = new UnsettledPaymentRow(1, "PM-1", "BK-1", Lot, purpose, PaymentMethod.Cash, 100_000, Utc(2026, 11, 3));
        var input = SettlementBuilder.Recognize(row, new Dictionary<string, BookingStatusInfo>(), Cutoff45);
        Assert.Equal(Utc(2026, 11, 3), input!.RecognizedAtUtc);
        Assert.Null(input.BlockedReason);
    }
}
