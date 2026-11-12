using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features.Commission;
using PaymentService.Application.Features.Reports;
using PaymentService.Domain.Entities;
using static PaymentService.Test.OwnerRevenueTestData;

namespace PaymentService.Test;

// US-079: báo cáo doanh thu chủ bãi (US-078) hiển thị hoa hồng tự tính; giao dịch chưa tính là "Pending", không bị coi là 0;
// block Settlement (quyết toán – US-080) giữ nguyên NotSettled / null.
public class OwnerRevenueCommissionTests
{
    private static readonly FixedTimeProvider Clock = new(Utc(2026, 10, 20));
    private static readonly OwnerRevenueQuery October = new(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1));

    // Bãi A1: 2 giao dịch đã tính (một đã hoàn 20.000 và đã bù). Bãi A2: 1 đã tính, 1 chưa tính. Chủ bãi B: không được lộ.
    private static readonly Payment A1Done = Paid(OwnerA, LotA1, 100_000, Utc(2026, 10, 5, 2)).WithId(1);
    private static readonly Payment A1Refunded = Paid(OwnerA, LotA1, 50_000, Utc(2026, 10, 5, 3), status: PaymentStatus.PartiallyRefunded,
        refunded: 20_000).WithId(2);
    private static readonly Payment A2Done = Paid(OwnerA, LotA2, 40_000, Utc(2026, 10, 6, 2)).WithId(3);
    private static readonly Payment A2Pending = Paid(OwnerA, LotA2, 30_000, Utc(2026, 10, 6, 3), PaymentMethod.Cash, PaymentPurpose.WalkIn)
        .WithId(4);
    private static readonly Payment BDone = Paid(OwnerB, LotB1, 900_000, Utc(2026, 10, 5, 2)).WithId(5);

    private static IEnumerable<CommissionEntry> Entries() =>
    [
        Accrual(A1Done, 0.075m, "LotContract", "HD-SP-2026-0011"),                 // 7.500
        Accrual(A1Refunded, 0.075m, "LotContract", "HD-SP-2026-0011"),             // 3.750
        Reversal(A1Refunded, 0.075m, refundedAmount: 20_000, source: "LotContract"), // −1.500
        Accrual(A2Done, 0.10m),                                                     // 4.000
        Accrual(BDone, 0.10m),                                                      // 90.000 – chủ bãi khác
    ];

    private static InMemoryOwnerRevenueQueries Queries(params Payment[] extra)
        => new([A1Done, A1Refunded, A2Done, A2Pending, BDone, .. extra], commissionEntries: Entries());

    [Fact]
    public async Task Summary_AddsCommissionBlock_WithPendingNotCountedAsZero()
    {
        var summary = await new GetOwnerRevenueSummaryUseCase(Queries(), Clock).ExecuteAsync(OwnerA, October);

        var commission = summary.Commission;
        Assert.Equal((CommissionStatuses.Partial, 13_750m, 1), (commission.Status, commission.CommissionAmount, commission.PendingCount));
        Assert.Equal(
        [
            new OwnerCommissionByLotDto(LotA1, 9_750, 0, CommissionStatuses.Calculated),
            new OwnerCommissionByLotDto(LotA2, 4_000, 1, CommissionStatuses.Partial),
        ], commission.ByLot);
        // Quyết toán (US-080) không bị ghi đè.
        Assert.Equal(new OwnerSettlementSummaryDto("NotSettled", 0, null, null, null, null), summary.Settlement);
    }

    [Fact]
    public async Task Summary_FilteredByLot_OnlyThatLot()
    {
        var summary = await new GetOwnerRevenueSummaryUseCase(Queries(), Clock).ExecuteAsync(OwnerA, October with { ParkingLotId = LotA2 });
        Assert.Equal(4_000m, summary.Commission.CommissionAmount);
        Assert.Equal(LotA2, Assert.Single(summary.Commission.ByLot).ParkingLotId);
    }

    [Fact]
    public async Task Summary_NoEntriesYet_IsPending()
    {
        var summary = await new GetOwnerRevenueSummaryUseCase(new InMemoryOwnerRevenueQueries([A1Done, A2Pending]), Clock)
            .ExecuteAsync(OwnerA, October);
        Assert.Equal((CommissionStatuses.Pending, 0m, 2), (summary.Commission.Status, summary.Commission.CommissionAmount, summary.Commission.PendingCount));
    }

    [Fact]
    public async Task Summary_RefundNotYetReversed_IsPending()
    {
        var lagging = Paid(OwnerA, LotA1, 10_000, Utc(2026, 10, 7, 2), status: PaymentStatus.PartiallyRefunded, refunded: 5_000).WithId(6);
        var queries = new InMemoryOwnerRevenueQueries([lagging], commissionEntries: [Accrual(lagging, 0.1m)]);

        var summary = await new GetOwnerRevenueSummaryUseCase(queries, Clock).ExecuteAsync(OwnerA, October);

        Assert.Equal((CommissionStatuses.Pending, 1_000m, 1), (summary.Commission.Status, summary.Commission.CommissionAmount, summary.Commission.PendingCount));
    }

    [Fact]
    public async Task Summary_ExcludedPurpose_IsNotPending()
    {
        var policy = new CommissionPolicy([PaymentPurpose.WalkIn]);
        var summary = await new GetOwnerRevenueSummaryUseCase(Queries(), Clock, policy).ExecuteAsync(OwnerA, October);
        Assert.Equal((CommissionStatuses.Calculated, 0), (summary.Commission.Status, summary.Commission.PendingCount));
    }

    [Fact]
    public async Task Series_PointsCarryCommissionAndPending_SumMatchesSummary()
    {
        var series = await new GetOwnerRevenueSeriesUseCase(Queries(), Clock)
            .ExecuteAsync(OwnerA, October with { From = new DateOnly(2026, 10, 5), To = new DateOnly(2026, 10, 8) }, "Day");

        Assert.Equal([9_750m, 4_000m, 0m], series.Points.Select(p => p.CommissionAmount));
        Assert.Equal([0, 1, 0], series.Points.Select(p => p.CommissionPendingCount));
    }

    [Fact]
    public async Task Series_Hour_PlacesCommissionInVietnamHour()
    {
        var series = await new GetOwnerRevenueSeriesUseCase(Queries(), Clock)
            .ExecuteAsync(OwnerA, October with { From = new DateOnly(2026, 10, 5), To = new DateOnly(2026, 10, 6) }, "Hour");

        Assert.Equal(7_500m, series.Points[9].CommissionAmount);                     // 02:00 UTC = 09:00 ICT
        Assert.Equal(2_250m, series.Points[10].CommissionAmount);                    // 03:00 UTC = 10:00 ICT (3.750 − 1.500)
        Assert.Equal(9_750m, series.Points.Sum(p => p.CommissionAmount));
    }

    [Fact]
    public async Task Transactions_ShowCommissionRateAndStatus_PerRow()
    {
        var page = await new ListOwnerRevenueTransactionsUseCase(Queries(), Clock)
            .ExecuteAsync(OwnerA, new OwnerRevenueTransactionQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1)), 1, 20);

        var byCode = page.Items.ToDictionary(i => i.Id);
        Assert.Equal((7_500m, 0.075m, CommissionStatuses.Calculated), (byCode[1].CommissionAmount, byCode[1].CommissionRate, byCode[1].CommissionStatus));
        Assert.Equal((2_250m, 0.075m, CommissionStatuses.Calculated), (byCode[2].CommissionAmount, byCode[2].CommissionRate, byCode[2].CommissionStatus));
        Assert.Equal((null as decimal?, null as decimal?, CommissionStatuses.Pending),
            (byCode[4].CommissionAmount, byCode[4].CommissionRate, byCode[4].CommissionStatus));
        Assert.DoesNotContain(5, byCode.Keys);
    }

    [Fact]
    public async Task Transactions_ExcludedPurposeWithoutAccrual_IsNotApplicable()
    {
        var page = await new ListOwnerRevenueTransactionsUseCase(Queries(), Clock, new CommissionPolicy([PaymentPurpose.WalkIn]))
            .ExecuteAsync(OwnerA, new OwnerRevenueTransactionQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1)), 1, 20);
        Assert.Equal(CommissionStatuses.NotApplicable, page.Items.Single(i => i.Id == 4).CommissionStatus);
    }

    [Theory]
    [InlineData(0, 0, CommissionStatuses.Calculated)]
    [InlineData(3, 0, CommissionStatuses.Calculated)]
    [InlineData(3, 1, CommissionStatuses.Partial)]
    [InlineData(3, 3, CommissionStatuses.Pending)]
    public void GroupStatus(int count, int pending, string expected) => Assert.Equal(expected, CommissionStatuses.ForGroup(count, pending));
}
