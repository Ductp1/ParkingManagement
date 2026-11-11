using System.Text;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Reports;
using PaymentService.Application.Features.Settlements;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Services;
using PaymentService.Infrastructure.Persistence.Queries;
using static PaymentService.Test.OwnerRevenueTestData;

namespace PaymentService.Test;

// US-080: luồng duyệt kỳ quyết toán (state machine), bút toán điều chỉnh có duyệt, đọc / CSV cho Admin và chủ bãi.
public class SettlementReviewTests
{
    private const int Submitter = 7;
    private const int Approver = 9;
    private static readonly DateTime Now = Utc(2026, 11, 10, 3);
    private static readonly DateOnly Week45 = new(2026, 11, 2);
    private static readonly FixedTimeProvider Clock = new(Now);
    private static readonly SettlementReviewOptions FourEyes = new(RequireDifferentApprover: true);

    private static string Rv(Settlement s) => Convert.ToBase64String(s.RowVersion);
    private static string Rv(FinancialAdjustment a) => Convert.ToBase64String(a.RowVersion);

    private static SettlementLine Line(SettlementLineType type, decimal amount, decimal commission = 0, string reference = "BK-1",
        DateTime? at = null, int? paymentId = 1)
        => new() { LineType = type, Amount = amount, CommissionAmount = commission, ReferenceCode = reference, ParkingLotId = LotA1,
            OccurredAtUtc = at ?? Utc(2026, 11, 4), PaymentId = type is SettlementLineType.DisputeHold ? null : paymentId };

    // ===================== State machine (domain) =====================

    [Fact]
    public void FullFlow_DraftSubmitApprovePaid()
    {
        var s = new InMemorySettlementReview().Add(OwnerA, Week45, 90_000);

        s.Submit(Submitter, Now);
        Assert.Equal(SettlementStatus.PendingReview, s.Status);
        Assert.Equal((Submitter, Now), (s.SubmittedByUserId!.Value, s.SubmittedAtUtc!.Value));
        Assert.Equal(["Approve", "Reject"], s.AllowedActions());

        s.Approve(Approver, Now.AddHours(1), requireDifferentApprover: true);
        Assert.Equal(SettlementStatus.Approved, s.Status);
        Assert.Equal(["MarkPaid"], s.AllowedActions());

        s.MarkPaid(Approver, "  FT26314XYZ  ", Now.AddHours(2), Now.AddHours(3));
        Assert.Equal(SettlementStatus.Paid, s.Status);
        Assert.Equal("FT26314XYZ", s.PayoutReference);
        Assert.Equal(Now.AddHours(2), s.PaidAtUtc);
        Assert.Empty(s.AllowedActions());
    }

    [Fact]
    public void Submit_BlockedOrNotDraft_Conflict()
    {
        var review = new InMemorySettlementReview();
        var blocked = review.Add(OwnerA, Week45, 90_000, blocked: SettlementBlockReasons.HoldsNotLoaded);
        Assert.Equal(["Regenerate"], blocked.AllowedActions());
        Assert.Contains("HoldsNotLoaded", Assert.Throws<ConflictException>(() => blocked.Submit(Submitter, Now)).Message);

        var approved = review.Add(OwnerB, Week45, 90_000, SettlementStatus.Approved);
        Assert.Throws<ConflictException>(() => approved.Submit(Submitter, Now));
    }

    [Fact]
    public void Approve_BySubmitter_ForbiddenWithFourEyes_AllowedWhenDisabled()
    {
        var s = new InMemorySettlementReview().Add(OwnerA, Week45, 90_000);
        s.Submit(Submitter, Now);

        Assert.Throws<ForbiddenException>(() => s.Approve(Submitter, Now, requireDifferentApprover: true));
        Assert.Equal(SettlementStatus.PendingReview, s.Status);
        s.Approve(Submitter, Now, requireDifferentApprover: false);
        Assert.Equal(SettlementStatus.Approved, s.Status);
    }

    [Fact]
    public void Approve_NotPending_Conflict()
        => Assert.Throws<ConflictException>(() => new InMemorySettlementReview().Add(OwnerA, Week45, 1).Approve(Approver, Now, true));

    [Fact]
    public void Reject_BackToDraftWithReason_ThenCanBeRecomputed()
    {
        var s = new InMemorySettlementReview().Add(OwnerA, Week45, 90_000);
        s.Submit(Submitter, Now);

        Assert.Throws<ValidationException>(() => s.Reject(Approver, "  ", Now));
        Assert.Throws<ValidationException>(() => s.Reject(Approver, new string('x', 501), Now));
        s.Reject(Approver, " Thiếu khoản giữ CP-0001 ", Now);

        Assert.Equal(SettlementStatus.Draft, s.Status);
        Assert.Equal("Thiếu khoản giữ CP-0001", s.RejectReason);
        Assert.Equal(Approver, s.RejectedByUserId);
        s.ApplyComputation(SettlementCalculator.Compute(new SettlementInput(Utc(2026, 11, 8, 17), Now, [], [], [], [], [], [], [], [], [], [], [], null)),
            Now, Approver, Now);
        Assert.Equal(0, s.NetPayout);
    }

    [Fact]
    public void AfterApproval_SettlementIsImmutable()
    {
        var s = new InMemorySettlementReview().Add(OwnerA, Week45, 90_000);
        s.Submit(Submitter, Now);
        s.Approve(Approver, Now, true);

        Assert.Throws<ConflictException>(() => s.ApplyComputation(
            SettlementCalculator.Compute(new SettlementInput(Utc(2026, 11, 8, 17), Now, [], [], [], [], [], [], [], [], [], [], [], null)), Now, 1, Now));
        Assert.Throws<ConflictException>(() => s.Reject(Approver, "muộn", Now));
        Assert.Equal(90_000, s.NetPayout);
    }

    [Fact]
    public void MarkPaid_Validation()
    {
        var review = new InMemorySettlementReview();
        Settlement Approved(decimal net)
        {
            var s = review.Add(OwnerA, Week45.AddDays(7 * review.Settlements.Count), net);
            s.Submit(Submitter, Now);
            s.Approve(Approver, Now, true);
            return s;
        }

        Assert.Throws<ConflictException>(() => review.Add(OwnerB, Week45, 1).MarkPaid(Approver, "FT1", Now, Now));       // chưa duyệt
        var negative = Approved(-10_000);
        Assert.Empty(negative.AllowedActions());
        Assert.Throws<ConflictException>(() => negative.MarkPaid(Approver, "FT1", Now, Now));                            // không có khoản chi
        var s = Approved(90_000);
        Assert.Throws<ValidationException>(() => s.MarkPaid(Approver, " ", Now, Now));
        Assert.Throws<ValidationException>(() => s.MarkPaid(Approver, new string('9', 101), Now, Now));
        Assert.Throws<ValidationException>(() => s.MarkPaid(Approver, "FT1", null, Now));
        Assert.Throws<ValidationException>(() => s.MarkPaid(Approver, "FT1", Now.AddHours(1), Now));                     // tương lai
        Assert.Throws<ValidationException>(() => s.MarkPaid(Approver, "FT1", Now.AddDays(-1), Now));                     // trước lúc duyệt
        Assert.Equal(SettlementStatus.Approved, s.Status);
    }

    // ===================== Bút toán điều chỉnh (domain) =====================

    [Theory]
    [InlineData(0, "Lý do")]
    [InlineData(1000.5, "Lý do")]
    [InlineData(10_000_000_001, "Lý do")]
    [InlineData(-5000, "")]
    public void CreateAdjustment_Invalid_Throws(decimal amount, string reason)
        => Assert.Throws<ValidationException>(() => FinancialAdjustment.CreateManual(OwnerA, amount, reason, Submitter, null, null, Now));

    [Fact]
    public void CreateAdjustment_WithSanction_IsPendingWithSource()
    {
        var a = FinancialAdjustment.CreateManual(OwnerA, -200_000, " Phạt SLA cấp 2 ", Submitter, 12, null, Now);
        Assert.Equal(FinancialAdjustmentStatus.PendingApproval, a.Status);
        Assert.Equal(FinancialAdjustmentSource.Sanction, a.SourceType);
        Assert.Equal("12", a.SourceReference);
        Assert.Equal("Phạt SLA cấp 2", a.Reason);
        Assert.Null(a.SettlementId);
    }

    [Fact]
    public void Reversal_MustMatchOwnerApprovalAndOppositeAmount()
    {
        var review = new InMemorySettlementReview();
        var approved = review.AddAdjustment(OwnerA, -50_000);
        var pending = review.AddAdjustment(OwnerA, -50_000, FinancialAdjustmentStatus.PendingApproval);

        Assert.Throws<ValidationException>(() => FinancialAdjustment.CreateManual(OwnerB, 50_000, "Đảo", Submitter, null, approved, Now));
        Assert.Throws<ConflictException>(() => FinancialAdjustment.CreateManual(OwnerA, 50_000, "Đảo", Submitter, null, pending, Now));
        Assert.Throws<ValidationException>(() => FinancialAdjustment.CreateManual(OwnerA, 40_000, "Đảo", Submitter, null, approved, Now));
        Assert.Equal(approved.Id, FinancialAdjustment.CreateManual(OwnerA, 50_000, "Đảo", Submitter, null, approved, Now).ReversesAdjustmentId);
    }

    [Fact]
    public void ApproveRejectAdjustment_FourEyesAndOnlyOnce()
    {
        var a = FinancialAdjustment.CreateManual(OwnerA, 30_000, "Bù hoa hồng tính thừa", Submitter, null, null, Now);
        Assert.Throws<ForbiddenException>(() => a.Approve(Submitter, Now, true));
        a.Approve(Approver, Now, true);
        Assert.Equal((FinancialAdjustmentStatus.Approved, Approver), (a.Status, a.ApprovedByUserId!.Value));
        Assert.Throws<ConflictException>(() => a.Approve(Approver, Now, true));
        Assert.Throws<ConflictException>(() => a.Reject(Approver, "x", Now));

        var b = FinancialAdjustment.CreateManual(OwnerA, 30_000, "Nhầm", Submitter, null, null, Now);
        Assert.Throws<ValidationException>(() => b.Reject(Approver, null, Now));
        b.Reject(Approver, "Trùng bút toán #5", Now);
        Assert.Equal(FinancialAdjustmentStatus.Rejected, b.Status);
    }

    // ===================== Use case chuyển trạng thái =====================

    [Fact]
    public async Task SubmitApproveMarkPaid_UseCases_StampRowVersionAndAudit()
    {
        var review = new InMemorySettlementReview();
        var s = review.Add(OwnerA, Week45, 90_000);

        var submitted = await new SubmitSettlementUseCase(review, Clock).ExecuteAsync(s.Id, Rv(s), Submitter);
        Assert.Equal("PendingReview", submitted.Status);
        Assert.Equal(Rv(s), submitted.RowVersion);

        await Assert.ThrowsAsync<ForbiddenException>(() => new ApproveSettlementUseCase(review, FourEyes, Clock).ExecuteAsync(s.Id, Rv(s), Submitter));
        var approved = await new ApproveSettlementUseCase(review, FourEyes, Clock).ExecuteAsync(s.Id, Rv(s), Approver);
        Assert.Equal("Approved", approved.Status);

        var paid = await new MarkSettlementPaidUseCase(review, Clock).ExecuteAsync(s.Id, Rv(s), "FT26314XYZ",
            new DateTime(2026, 11, 10, 3, 2, 0, DateTimeKind.Unspecified), Approver);
        Assert.Equal("Paid", paid.Status);
        Assert.Equal(Utc(2026, 11, 10, 3, 2), s.PaidAtUtc);                                  // không ghi múi giờ → hiểu là UTC
        Assert.Equal(["Settlement.Submitted", "Settlement.Approved", "Settlement.Paid"],
            review.Outbox.Select(o => System.Text.Json.JsonDocument.Parse(o.PayloadJson).RootElement.GetProperty("action").GetString()));
    }

    [Fact]
    public async Task Transition_StaleRowVersion_Conflict_NothingChanged()
    {
        var review = new InMemorySettlementReview();
        var s = review.Add(OwnerA, Week45, 90_000);
        var stale = Rv(s);
        await new SubmitSettlementUseCase(review, Clock).ExecuteAsync(s.Id, stale, Submitter);

        await Assert.ThrowsAsync<ConflictException>(() => new RejectSettlementUseCase(review, Clock).ExecuteAsync(s.Id, stale, "x", Approver));
        Assert.Equal(SettlementStatus.PendingReview, s.Status);
        Assert.Single(review.Outbox);
    }

    [Fact]
    public async Task Transition_Guards()
    {
        var review = new InMemorySettlementReview();
        var s = review.Add(OwnerA, Week45, 90_000);
        var submit = new SubmitSettlementUseCase(review, Clock);

        await Assert.ThrowsAsync<AuthenticationException>(() => submit.ExecuteAsync(s.Id, Rv(s), null));
        await Assert.ThrowsAsync<ValidationException>(() => submit.ExecuteAsync(s.Id, null, Submitter));
        await Assert.ThrowsAsync<ValidationException>(() => submit.ExecuteAsync(0, Rv(s), Submitter));
        await Assert.ThrowsAsync<NotFoundException>(() => submit.ExecuteAsync(99, Rv(s), Submitter));
        await Assert.ThrowsAsync<ConflictException>(() =>
            new MarkSettlementPaidUseCase(review, Clock).ExecuteAsync(s.Id, Rv(s), "FT1", Now, Approver));     // Draft → không chi được
    }

    // ===================== Use case bút toán =====================

    [Fact]
    public async Task CreateAdjustment_PendingWithAuditId_ThenApprovedByAnotherAdmin()
    {
        var review = new InMemorySettlementReview();

        var created = await new CreateAdjustmentUseCase(review, Clock).ExecuteAsync(
            new CreateAdjustmentCommand(OwnerA, -150_000, "Chargeback VNPay BK-0002", null, null), Submitter);

        Assert.Equal("PendingApproval", created.Status);
        Assert.Equal("Manual", created.SourceType);
        Assert.Null(created.SettlementId);
        var audit = System.Text.Json.JsonDocument.Parse(Assert.Single(review.Outbox).PayloadJson).RootElement;
        Assert.Equal("FinancialAdjustment.Created", audit.GetProperty("action").GetString());
        Assert.Equal(created.Id.ToString(), audit.GetProperty("entityId").GetString());

        var adjustment = review.Adjustments.Single();
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ApproveAdjustmentUseCase(review, FourEyes, Clock).ExecuteAsync(created.Id, Rv(adjustment), Submitter));
        var approved = await new ApproveAdjustmentUseCase(review, FourEyes, Clock).ExecuteAsync(created.Id, Rv(adjustment), Approver);
        Assert.Equal("Approved", approved.Status);
        Assert.Equal(Approver, approved.ApprovedByUserId);
    }

    [Fact]
    public async Task CreateAdjustment_ReversalOnlyOnce()
    {
        var review = new InMemorySettlementReview();
        var original = review.AddAdjustment(OwnerA, -50_000);
        var create = new CreateAdjustmentUseCase(review, Clock);

        var reversal = await create.ExecuteAsync(new CreateAdjustmentCommand(OwnerA, 50_000, "Đảo bút toán nhập nhầm", null, original.Id), Submitter);
        Assert.Equal(original.Id, reversal.ReversesAdjustmentId);
        await Assert.ThrowsAsync<ConflictException>(() =>
            create.ExecuteAsync(new CreateAdjustmentCommand(OwnerA, 50_000, "Đảo lần 2", null, original.Id), Submitter));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            create.ExecuteAsync(new CreateAdjustmentCommand(OwnerA, 50_000, "Đảo", null, 9999), Submitter));
    }

    [Theory]
    [InlineData(null, 1000)]
    [InlineData(1, null)]
    public async Task CreateAdjustment_MissingOwnerOrAmount_Validation(int? owner, int? amount)
        => await Assert.ThrowsAsync<ValidationException>(() => new CreateAdjustmentUseCase(new InMemorySettlementReview(), Clock)
            .ExecuteAsync(new CreateAdjustmentCommand(owner, amount, "Lý do", null, null), Submitter));

    [Fact]
    public async Task ListAdjustments_FilterAttachedStatusOwner_WithSettlementCode()
    {
        var review = new InMemorySettlementReview();
        var s = review.Add(OwnerA, Week45, 1);
        review.AddAdjustment(OwnerA, 10_000, settlementId: s.Id);
        review.AddAdjustment(OwnerA, 20_000);
        review.AddAdjustment(OwnerA, 30_000, FinancialAdjustmentStatus.PendingApproval);
        review.AddAdjustment(OwnerB, 40_000);
        var list = new ListAdjustmentsUseCase(review);

        var attached = await list.ExecuteAsync(new AdjustmentListQuery(OwnerA, null, true, null), 1, 20);
        Assert.Equal(s.Code, Assert.Single(attached.Items).SettlementCode);
        var unattachedApproved = await list.ExecuteAsync(new AdjustmentListQuery(OwnerA, "approved", false, null), 1, 20);
        Assert.Equal(20_000, Assert.Single(unattachedApproved.Items).Amount);
        Assert.Equal(4, (await list.ExecuteAsync(new AdjustmentListQuery(null, null, null, null), 1, 20)).TotalCount);
        await Assert.ThrowsAsync<ValidationException>(() => list.ExecuteAsync(new AdjustmentListQuery(null, "Paid", null, null), 1, 20));
    }

    // ===================== Đọc (Admin) =====================

    [Fact]
    public async Task List_FiltersSearchAndPaging()
    {
        var review = new InMemorySettlementReview();
        review.Add(OwnerA, Week45, 90_000, SettlementStatus.PendingReview);
        review.Add(OwnerB, Week45, 50_000, blocked: SettlementBlockReasons.CommissionPending);
        review.Add(OwnerA, Week45.AddDays(-7), 70_000, SettlementStatus.Paid);
        var list = new ListSettlementsUseCase(review);

        var pending = await list.ExecuteAsync(new SettlementListQuery("pendingreview", null, null, null, null), 1, 20);
        Assert.Equal("STL-20261102-0001", Assert.Single(pending.Items).Code);
        Assert.Equal(OwnerB, Assert.Single((await list.ExecuteAsync(new SettlementListQuery(null, null, null, null, true), 1, 20)).Items).OwnerProfileId);
        Assert.Equal(2, (await list.ExecuteAsync(new SettlementListQuery(null, null, null, "1", null), 1, 20)).TotalCount);       // số chủ bãi
        Assert.Single((await list.ExecuteAsync(new SettlementListQuery(null, null, null, "stl-20261026", null), 1, 20)).Items);    // mã kỳ
        Assert.Equal(2, (await list.ExecuteAsync(new SettlementListQuery(null, null, Week45, null, null), 1, 20)).TotalCount);

        var page2 = await list.ExecuteAsync(new SettlementListQuery(null, null, null, null, null), 2, 2);
        Assert.Equal((3, 1), (page2.TotalCount, page2.Items.Count));
        Assert.Equal(Week45.AddDays(-7), page2.Items[0].PeriodStart);                                                              // mới nhất trước

        await Assert.ThrowsAsync<ValidationException>(() => list.ExecuteAsync(new SettlementListQuery("Closed", null, null, null, null), 1, 20));
        await Assert.ThrowsAsync<ValidationException>(() => list.ExecuteAsync(new SettlementListQuery(null, null, null, null, null), 1, 101));
        await Assert.ThrowsAsync<ValidationException>(() => list.ExecuteAsync(new SettlementListQuery(null, 0, null, null, null), 1, 20));
    }

    [Fact]
    public async Task Stats_CountsByStatusBlockedAndAmounts()
    {
        var review = new InMemorySettlementReview();
        review.Add(OwnerA, Week45, 90_000, SettlementStatus.PendingReview);
        review.Add(OwnerB, Week45, 50_000, blocked: "HoldsNotLoaded");
        review.Add(3, Week45, 30_000, SettlementStatus.Approved);
        review.Add(4, Week45, -5_000, SettlementStatus.Approved);
        review.Add(5, Week45.AddDays(-7), 70_000, SettlementStatus.Paid);

        var all = await new GetSettlementStatsUseCase(review).ExecuteAsync(null);
        Assert.Equal(new SettlementStatsDto(1, 1, 2, 1, 1, 90_000, 30_000), all);
        Assert.Equal(0, (await new GetSettlementStatsUseCase(review).ExecuteAsync(Week45)).Paid);
    }

    [Fact]
    public async Task Detail_FormulaTimelineTotalsAdjustmentsActions()
    {
        var review = new InMemorySettlementReview();
        var s = review.Add(OwnerA, Week45, 60_000, SettlementStatus.Draft, null,
            Line(SettlementLineType.Booking, 100_000, 10_000), Line(SettlementLineType.Refund, -20_000, -2_000),
            Line(SettlementLineType.DisputeHold, -30_000, reference: "CP-0001"));
        s.RejectedAtUtc = Now.AddHours(1);
        s.RejectedByUserId = Approver;
        s.RejectReason = "Thiếu hold";
        review.AddAdjustment(OwnerA, 5_000, settlementId: s.Id);

        var detail = await new GetSettlementUseCase(review).ExecuteAsync(s.Id);

        Assert.Equal(SettlementFormulaDto.Text, detail.Formula.Expression);
        Assert.Equal(["Generated", "Rejected"], detail.Timeline.Select(t => t.Event));
        Assert.Equal("Thiếu hold", detail.Timeline[1].Note);
        Assert.Equal(["Booking", "Refund", "DisputeHold"], detail.LineTotals.Select(t => t.LineType));
        Assert.Equal(-2_000, detail.LineTotals.Single(t => t.LineType == "Refund").CommissionAmount);
        Assert.Equal(5_000, Assert.Single(detail.Adjustments).Amount);
        Assert.Equal(["Regenerate", "Submit"], detail.AllowedActions);
        await Assert.ThrowsAsync<NotFoundException>(() => new GetSettlementUseCase(review).ExecuteAsync(999));
    }

    [Fact]
    public async Task Lines_PagedAndFilteredByType()
    {
        var review = new InMemorySettlementReview();
        var s = review.Add(OwnerA, Week45, 1, SettlementStatus.Draft, null,
            Line(SettlementLineType.Booking, 100_000, at: Utc(2026, 11, 5), paymentId: 1), Line(SettlementLineType.Booking, 50_000, at: Utc(2026, 11, 3), paymentId: 2),
            Line(SettlementLineType.Refund, -20_000));
        var lines = new ListSettlementLinesUseCase(review);

        var bookings = await lines.ExecuteAsync(s.Id, "booking", 1, 50);
        Assert.Equal([2, 1], bookings.Items.Select(l => l.PaymentId!.Value));                                   // theo thời điểm phát sinh
        Assert.Equal(3, (await lines.ExecuteAsync(s.Id, null, 1, 50)).TotalCount);
        await Assert.ThrowsAsync<ValidationException>(() => lines.ExecuteAsync(s.Id, "Payout", 1, 50));
        await Assert.ThrowsAsync<NotFoundException>(() => lines.ExecuteAsync(999, null, 1, 50));
    }

    [Fact]
    public async Task ExportCsv_ColumnsBomAndInjectionGuard()
    {
        var review = new InMemorySettlementReview();
        var s = review.Add(OwnerA, Week45, 90_000, SettlementStatus.Paid, null, Line(SettlementLineType.Booking, 100_000, 10_000, reference: "=HYPERLINK(1)"));
        s.PayoutReference = "+FT1";

        var list = await new ExportSettlementsUseCase(review).ExecuteAsync(new SettlementListQuery(null, null, Week45, null, null));
        Assert.Equal("settlements-20261102.csv", list.FileName);
        Assert.Equal(Encoding.UTF8.GetPreamble(), list.Content[..3]);
        var text = Encoding.UTF8.GetString(list.Content[3..]).Split("\r\n");
        Assert.Equal(string.Join(",", SettlementCsvWriter.SettlementHeader), text[0]);
        Assert.Equal("Code,OwnerProfileId,PeriodStart,PeriodEnd,Status,GrossAmount,RefundAmount,OwnerCollectedAmount,CommissionAmount,HeldAmount,"
                     + "AdjustmentAmount,CarryOverAmount,NetPayout,IsBlocked,BlockedReasons,ApprovedAtUtc,PaidAtUtc,PayoutReference", text[0]);
        Assert.StartsWith("STL-20261102-0001,1,2026-11-02,2026-11-08,Paid,100000,", text[1]);
        Assert.EndsWith(",'+FT1", text[1]);

        var lines = await new ExportSettlementLinesUseCase(review).ExecuteAsync(s.Id);
        Assert.Equal("STL-20261102-0001.csv", lines.FileName);
        var row = Encoding.UTF8.GetString(lines.Content[3..]).Split("\r\n")[1];
        Assert.Equal("STL-20261102-0001,Booking,'=HYPERLINK(1),11,1,,2026-11-04T00:00:00Z,04/11/2026 07:00:00,100000,10000,false,", row);
    }

    [Fact]
    public async Task ExportCsv_OverLimit_Validation()
    {
        var review = new InMemorySettlementReview();
        for (var i = 0; i <= SettlementCsvFile.MaxRows; i++) review.Add(i + 1, Week45, 1);
        await Assert.ThrowsAsync<ValidationException>(() =>
            new ExportSettlementsUseCase(review).ExecuteAsync(new SettlementListQuery(null, null, null, null, null)));
    }

    // ===================== Chủ bãi =====================

    [Fact]
    public async Task Owner_SeesOnlyOwnApprovedOrPaid()
    {
        var review = new InMemorySettlementReview();
        var paid = review.Add(OwnerA, Week45.AddDays(-7), 70_000, SettlementStatus.Paid, null, Line(SettlementLineType.Booking, 80_000, 10_000));
        var draft = review.Add(OwnerA, Week45, 90_000, SettlementStatus.PendingReview);
        var other = review.Add(OwnerB, Week45.AddDays(-7), 50_000, SettlementStatus.Approved);
        review.AddAdjustment(OwnerA, 5_000, settlementId: paid.Id, createdBy: 42);

        var list = await new ListOwnerSettlementsUseCase(review).ExecuteAsync(OwnerA, 1, 20);
        Assert.Equal(paid.Id, Assert.Single(list.Items).Id);

        var detail = await new GetOwnerSettlementUseCase(review).ExecuteAsync(OwnerA, paid.Id);
        Assert.Equal(5_000, Assert.Single(detail.Adjustments).Amount);
        Assert.DoesNotContain("CreatedByUserId", System.Text.Json.JsonSerializer.Serialize(detail));                   // không lộ Id Admin

        await Assert.ThrowsAsync<NotFoundException>(() => new GetOwnerSettlementUseCase(review).ExecuteAsync(OwnerA, draft.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => new GetOwnerSettlementUseCase(review).ExecuteAsync(OwnerA, other.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => new ListOwnerSettlementLinesUseCase(review).ExecuteAsync(OwnerA, other.Id, null, 1, 20));
        await Assert.ThrowsAsync<NotFoundException>(() => new ExportOwnerSettlementLinesUseCase(review).ExecuteAsync(OwnerA, other.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => new ListOwnerSettlementsUseCase(review).ExecuteAsync(null, 1, 20));

        Assert.Single((await new ListOwnerSettlementLinesUseCase(review).ExecuteAsync(OwnerA, paid.Id, null, 1, 20)).Items);
        Assert.Equal($"{paid.Code}.csv", (await new ExportOwnerSettlementLinesUseCase(review).ExecuteAsync(OwnerA, paid.Id)).FileName);
    }

    // ===================== US-078: block quyết toán của báo cáo doanh thu =====================

    [Fact]
    public void OwnerRevenueReport_ShowsSettlementOnlyAfterApproval()
    {
        var review = new InMemorySettlementReview();
        var s = review.Add(OwnerA, Week45, 85_000, SettlementStatus.Draft, null,
            Line(SettlementLineType.Booking, 100_000, 10_000), Line(SettlementLineType.Refund, -10_000, -1_000));
        s.CommissionAmount = 9_000;
        var filter = new OwnerRevenueFilter(OwnerA, null, new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1));

        Assert.Empty(OwnerRevenueQueryShapes.SettledPeriods(review.Settlements.AsQueryable(), filter));

        s.Submit(Submitter, Now);
        s.Approve(Approver, Now, true);
        var row = Assert.Single(OwnerRevenueQueryShapes.SettledPeriods(review.Settlements.AsQueryable(), filter));
        Assert.Equal(new OwnerSettlementRow(Week45, new DateOnly(2026, 11, 8), 9_000, 85_000), row);

        var byLot = Assert.Single(OwnerRevenueQueryShapes.SettledPeriods(review.Settlements.AsQueryable(), filter with { ParkingLotId = LotA1 }));
        Assert.Equal(9_000, byLot.CommissionAmount);                                         // hoa hồng theo bãi = Σ dòng (đã trừ phần hoàn)
    }
}
