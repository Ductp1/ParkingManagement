using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Reports;
using PaymentService.Domain.Entities;
using static PaymentService.Test.OwnerRevenueTestData;

namespace PaymentService.Test;

/// <summary>
/// US-078 (UC-35): báo cáo doanh thu tổng hợp của chủ bãi. Port chạy trong bộ nhớ bằng đúng hình dạng truy vấn của Infrastructure.
/// Khoảng mặc định trong test: tháng 10/2026 theo giờ Việt Nam = [2026-09-30T17:00Z, 2026-10-31T17:00Z).
/// </summary>
public class OwnerRevenueSummaryTests
{
    private static readonly DateOnly Oct1 = new(2026, 10, 1);
    private static readonly DateOnly Nov1 = new(2026, 11, 1);
    private static readonly OwnerRevenueQuery October = new(Oct1, Nov1);

    private static GetOwnerRevenueSummaryUseCase UseCase(IOwnerRevenueQueries queries, DateTime? nowUtc = null)
        => new(queries, new FixedTimeProvider(nowUtc ?? Utc(2026, 10, 15, 3)));

    // ----- AC2: multi-tenant isolation -----

    [Fact]
    public async Task OwnerSeesOnlyOwnPayments()
    {
        var queries = new InMemoryOwnerRevenueQueries(
        [
            Paid(OwnerA, LotA1, 100_000, Utc(2026, 10, 5, 2)),
            Paid(OwnerA, LotA2, 50_000, Utc(2026, 10, 6, 2)),
            Paid(OwnerB, LotB1, 70_000, Utc(2026, 10, 6, 2)),
        ]);

        var summary = await UseCase(queries).ExecuteAsync(OwnerA, October);

        Assert.Equal(new OwnerRevenueTotalsDto(150_000, 0, 150_000, 2), summary.Totals);
        Assert.Equal([LotA1, LotA2], summary.ByLot.Select(l => l.ParkingLotId));
        Assert.Equal(OwnerA, queries.LastFilter!.OwnerProfileId);
    }

    [Fact]
    public async Task ParkingLotOfAnotherOwner_ReturnsZeros_NotNotFound()
    {
        var queries = new InMemoryOwnerRevenueQueries(
            [Paid(OwnerB, LotB1, 70_000, Utc(2026, 10, 6, 2)), Paid(OwnerA, LotA1, 100_000, Utc(2026, 10, 5, 2))],
            [Settlement(OwnerB, Oct1.AddDays(4), Oct1.AddDays(10), SettlementStatus.Approved, 7_000, 63_000, (LotB1, 7_000))]);

        var summary = await UseCase(queries).ExecuteAsync(OwnerA, October with { ParkingLotId = LotB1 });

        Assert.Equal(new OwnerRevenueTotalsDto(0, 0, 0, 0), summary.Totals);
        Assert.Empty(summary.ByLot);
        Assert.All(summary.ByMethod, m => Assert.Equal(0, m.TransactionCount));
        Assert.All(summary.ByHourOfDay, h => Assert.Equal(0, h.TransactionCount));
        Assert.Equal(OwnerRevenueRules.NotSettled, summary.Settlement.SettlementStatus);
        Assert.Equal((OwnerA, LotB1), (queries.LastFilter!.OwnerProfileId, queries.LastFilter.ParkingLotId));
    }

    [Fact]
    public async Task FilterByOwnLot_OnlyThatLot()
    {
        var queries = new InMemoryOwnerRevenueQueries(
            [Paid(OwnerA, LotA1, 100_000, Utc(2026, 10, 5, 2)), Paid(OwnerA, LotA2, 50_000, Utc(2026, 10, 6, 2))]);

        var summary = await UseCase(queries).ExecuteAsync(OwnerA, October with { ParkingLotId = LotA2 });

        Assert.Equal(new OwnerRevenueTotalsDto(50_000, 0, 50_000, 1), summary.Totals);
        Assert.Equal(LotA2, Assert.Single(summary.ByLot).ParkingLotId);
        Assert.Equal(LotA2, summary.ParkingLotId);
    }

    // ----- Trạng thái, ranh giới ngày, hoàn tiền -----

    [Fact]
    public async Task CountsOnlySucceededPartiallyRefundedAndRefunded()
    {
        var at = Utc(2026, 10, 5, 2);
        var queries = new InMemoryOwnerRevenueQueries(
        [
            Paid(OwnerA, LotA1, 1_000, at, status: PaymentStatus.Succeeded),
            Paid(OwnerA, LotA1, 2_000, at, status: PaymentStatus.PartiallyRefunded, refunded: 500),
            Paid(OwnerA, LotA1, 4_000, at, status: PaymentStatus.Refunded, refunded: 4_000),
            Paid(OwnerA, LotA1, 8_000, at, status: PaymentStatus.Pending),
            Paid(OwnerA, LotA1, 16_000, at, status: PaymentStatus.Failed),
            Paid(OwnerA, LotA1, 32_000, at, status: PaymentStatus.Cancelled),
        ]);

        var summary = await UseCase(queries).ExecuteAsync(OwnerA, October);

        Assert.Equal(new OwnerRevenueTotalsDto(7_000, 4_500, 2_500, 3), summary.Totals);
    }

    [Fact]
    public async Task DayBoundaries_FollowVietnamTime_FromInclusiveToExclusive()
    {
        var queries = new InMemoryOwnerRevenueQueries(
        [
            Paid(OwnerA, LotA1, 1, Utc(2026, 9, 30, 16, 59, 59)),   // 30/09 23:59:59 ICT → ngoài
            Paid(OwnerA, LotA1, 10, Utc(2026, 9, 30, 17)),          // 01/10 00:00:00 ICT → trong
            Paid(OwnerA, LotA1, 100, Utc(2026, 10, 31, 16, 59, 59)), // 31/10 23:59:59 ICT → trong
            Paid(OwnerA, LotA1, 1_000, Utc(2026, 10, 31, 17)),       // 01/11 00:00:00 ICT → ngoài (to loại trừ)
        ]);

        var summary = await UseCase(queries).ExecuteAsync(OwnerA, October);

        Assert.Equal(110, summary.Totals.GrossRevenue);
        Assert.Equal(2, summary.Totals.TransactionCount);
        Assert.Equal(Utc(2026, 9, 30, 17), queries.LastFilter!.FromUtc);
        Assert.Equal(Utc(2026, 10, 31, 17), queries.LastFilter.ToUtc);
    }

    [Fact]
    public async Task PartialAndFullRefunds_CountInOriginalPaymentPeriod()
    {
        var queries = new InMemoryOwnerRevenueQueries(
        [
            Paid(OwnerA, LotA1, 100_000, Utc(2026, 10, 5, 2), status: PaymentStatus.PartiallyRefunded, refunded: 30_000),
            Paid(OwnerA, LotA1, 50_000, Utc(2026, 10, 6, 2), status: PaymentStatus.Refunded, refunded: 50_000),
        ]);

        var summary = await UseCase(queries).ExecuteAsync(OwnerA, October);

        Assert.Equal(new OwnerRevenueTotalsDto(150_000, 80_000, 70_000, 2), summary.Totals);
        Assert.Equal("OriginalPaymentPeriod", summary.RefundBasis);
        Assert.Equal("VND", summary.Currency);
    }

    [Fact]
    public async Task Amounts_AreRoundedToWholeVnd()
    {
        var queries = new InMemoryOwnerRevenueQueries([Paid(OwnerA, LotA1, 10_000.5m, Utc(2026, 10, 5, 2))]);

        var summary = await UseCase(queries).ExecuteAsync(OwnerA, October);

        Assert.Equal(10_001, summary.Totals.GrossRevenue);
    }

    // ----- Breakdown -----

    [Fact]
    public async Task BreakdownByMethodAndPurpose_ListsEveryValue()
    {
        var at = Utc(2026, 10, 5, 2);
        var queries = new InMemoryOwnerRevenueQueries(
        [
            Paid(OwnerA, LotA1, 100_000, at, PaymentMethod.Vnpay, PaymentPurpose.Booking),
            Paid(OwnerA, LotA1, 20_000, at, PaymentMethod.Cash, PaymentPurpose.Overstay),
            Paid(OwnerA, LotA2, 30_000, at, PaymentMethod.VietQr, PaymentPurpose.WalkIn, PaymentStatus.PartiallyRefunded, 10_000),
            Paid(OwnerA, LotA2, 40_000, at, PaymentMethod.Vnpay, PaymentPurpose.Extension),
        ]);

        var summary = await UseCase(queries).ExecuteAsync(OwnerA, October);

        Assert.Equal(["Vnpay", "Momo", "VietQr", "Cash"], summary.ByMethod.Select(m => m.Key));
        Assert.Equal(new OwnerRevenueByKeyDto("Vnpay", 140_000, 0, 140_000, 2), summary.ByMethod[0]);
        Assert.Equal(new OwnerRevenueByKeyDto("Momo", 0, 0, 0, 0), summary.ByMethod[1]);
        Assert.Equal(new OwnerRevenueByKeyDto("VietQr", 30_000, 10_000, 20_000, 1), summary.ByMethod[2]);

        Assert.Equal(["Booking", "Extension", "Overstay", "WalkIn", "MonthlyPass"], summary.ByPurpose.Select(p => p.Key));
        Assert.Equal(40_000, summary.ByPurpose.Single(p => p.Key == "Extension").GrossRevenue);

        Assert.Equal([new OwnerRevenueByLotDto(LotA1, 120_000, 0, 120_000, 2), new OwnerRevenueByLotDto(LotA2, 70_000, 10_000, 60_000, 2)],
            summary.ByLot);
    }

    [Fact]
    public async Task ByHourOfDay_Has24VietnamHours()
    {
        var queries = new InMemoryOwnerRevenueQueries(
        [
            Paid(OwnerA, LotA1, 30_000, Utc(2026, 10, 5, 1, 30)),     // 08:30 ICT
            Paid(OwnerA, LotA1, 20_000, Utc(2026, 10, 6, 1, 5), status: PaymentStatus.PartiallyRefunded, refunded: 5_000), // 08:05 ICT
            Paid(OwnerA, LotA1, 10_000, Utc(2026, 10, 6, 17, 10)),    // 00:10 ICT ngày 07/10
        ]);

        var summary = await UseCase(queries).ExecuteAsync(OwnerA, October);

        Assert.Equal(Enumerable.Range(0, 24), summary.ByHourOfDay.Select(h => h.Hour));
        Assert.Equal(new OwnerRevenueByHourDto(8, 50_000, 45_000, 2), summary.ByHourOfDay[8]);
        Assert.Equal(new OwnerRevenueByHourDto(0, 10_000, 10_000, 1), summary.ByHourOfDay[0]);
        Assert.Equal(new OwnerRevenueByHourDto(1, 0, 0, 0), summary.ByHourOfDay[1]);
    }

    [Fact]
    public async Task ByRefundReason_CountsCompletedRefundsOfPaymentsInRange()
    {
        var inRange = Paid(OwnerA, LotA1, 100_000, Utc(2026, 10, 5, 2), status: PaymentStatus.PartiallyRefunded, refunded: 40_000);
        inRange.Refunds.Add(new Refund { Amount = 30_000, Reason = RefundReason.CustomerCancel, Status = RefundStatus.Completed });
        inRange.Refunds.Add(new Refund { Amount = 10_000, Reason = RefundReason.LotFault, Status = RefundStatus.Completed });
        inRange.Refunds.Add(new Refund { Amount = 5_000, Reason = RefundReason.CustomerCancel, Status = RefundStatus.Requested });
        var outOfRange = Paid(OwnerA, LotA1, 50_000, Utc(2026, 11, 2, 2), status: PaymentStatus.Refunded, refunded: 50_000);
        outOfRange.Refunds.Add(new Refund { Amount = 50_000, Reason = RefundReason.CustomerCancel, Status = RefundStatus.Completed });

        var summary = await UseCase(new InMemoryOwnerRevenueQueries([inRange, outOfRange])).ExecuteAsync(OwnerA, October);

        Assert.Equal(Enum.GetNames<RefundReason>(), summary.ByRefundReason.Select(r => r.Reason));
        Assert.Equal(new OwnerRefundByReasonDto("CustomerCancel", 1, 30_000), summary.ByRefundReason.Single(r => r.Reason == "CustomerCancel"));
        Assert.Equal(new OwnerRefundByReasonDto("LotFault", 1, 10_000), summary.ByRefundReason.Single(r => r.Reason == "LotFault"));
    }

    // ----- Hoa hồng / khoản nhận về (chỉ từ kỳ quyết toán đã duyệt – US-080) -----

    [Fact]
    public async Task NoApprovedSettlement_CommissionAndPayoutAreNull()
    {
        var queries = new InMemoryOwnerRevenueQueries(
            [Paid(OwnerA, LotA1, 100_000, Utc(2026, 10, 5, 2))],
            [
                Settlement(OwnerA, Oct1.AddDays(4), Oct1.AddDays(10), SettlementStatus.Draft, 10_000, 90_000),
                Settlement(OwnerA, Oct1.AddDays(11), Oct1.AddDays(17), SettlementStatus.PendingReview, 10_000, 90_000),
            ]);

        var summary = await UseCase(queries).ExecuteAsync(OwnerA, October);

        Assert.Equal(new OwnerSettlementSummaryDto("NotSettled", 0, null, null, null, null), summary.Settlement);
    }

    [Fact]
    public async Task ApprovedAndPaidSettlementsInsideRange_AreSummed()
    {
        var queries = new InMemoryOwnerRevenueQueries(settlements:
        [
            Settlement(OwnerA, Oct1.AddDays(4), Oct1.AddDays(10), SettlementStatus.Paid, 10_000, 90_000),
            Settlement(OwnerA, Oct1.AddDays(11), Oct1.AddDays(17), SettlementStatus.Approved, 5_000, 45_000),
            Settlement(OwnerA, new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 4), SettlementStatus.Paid, 1, 1),   // vắt qua from
            Settlement(OwnerA, new DateOnly(2026, 10, 26), Nov1, SettlementStatus.Paid, 1, 1),                    // PeriodEnd = to
            Settlement(OwnerB, Oct1.AddDays(4), Oct1.AddDays(10), SettlementStatus.Paid, 99, 99),
        ]);

        var summary = await UseCase(queries).ExecuteAsync(OwnerA, October);

        Assert.Equal(new OwnerSettlementSummaryDto("Settled", 2, Oct1.AddDays(4), Oct1.AddDays(17), 15_000, 135_000), summary.Settlement);
    }

    [Fact]
    public async Task SettlementFilteredByLot_UsesLotLines_AndPayoutIsNull()
    {
        var queries = new InMemoryOwnerRevenueQueries(settlements:
        [
            Settlement(OwnerA, Oct1.AddDays(4), Oct1.AddDays(10), SettlementStatus.Paid, 15_000, 135_000, (LotA1, 10_000), (LotA2, 5_000)),
        ]);

        var summary = await UseCase(queries).ExecuteAsync(OwnerA, October with { ParkingLotId = LotA2 });

        Assert.Equal(new OwnerSettlementSummaryDto("Settled", 1, Oct1.AddDays(4), Oct1.AddDays(10), 5_000, null), summary.Settlement);
    }

    // ----- Kiểm tra tham số -----

    [Theory]
    [InlineData("2026-10-10", "2026-10-10", "from phải nhỏ hơn to.")]
    [InlineData("2026-10-10", "2026-10-01", "from phải nhỏ hơn to.")]
    [InlineData("2025-10-01", "2026-10-03", "Khoảng thời gian báo cáo tối đa 366 ngày.")]
    public async Task InvalidRange_Throws400(string from, string to, string message)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => UseCase(new InMemoryOwnerRevenueQueries())
            .ExecuteAsync(OwnerA, new OwnerRevenueQuery(DateOnly.Parse(from), DateOnly.Parse(to))));
        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public async Task Range366Days_IsAllowed()
    {
        var summary = await UseCase(new InMemoryOwnerRevenueQueries())
            .ExecuteAsync(OwnerA, new OwnerRevenueQuery(new DateOnly(2025, 10, 1), new DateOnly(2026, 10, 2)));
        Assert.Equal(366, summary.To.DayNumber - summary.From.DayNumber);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task NonPositiveParkingLotId_Throws400(int lotId)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => UseCase(new InMemoryOwnerRevenueQueries())
            .ExecuteAsync(OwnerA, October with { ParkingLotId = lotId }));
        Assert.Equal("parkingLotId phải là số nguyên dương.", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task OwnerWithoutProfile_Throws403(int? ownerProfileId)
    {
        var queries = new InMemoryOwnerRevenueQueries();
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => UseCase(queries).ExecuteAsync(ownerProfileId, October));
        Assert.Equal("Tài khoản chưa gắn với một hồ sơ chủ bãi.", ex.Message);
        Assert.Null(queries.LastFilter);
    }

    [Fact]
    public async Task NoDates_DefaultsToCurrentMonthInVietnamTime()
    {
        // 31/10/2026 17:30 UTC = 01/11/2026 00:30 ICT → tháng 11.
        var summary = await UseCase(new InMemoryOwnerRevenueQueries(), Utc(2026, 10, 31, 17, 30)).ExecuteAsync(OwnerA, new OwnerRevenueQuery());

        Assert.Equal((Nov1, new DateOnly(2026, 12, 1)), (summary.From, summary.To));
    }

    [Fact]
    public async Task OnlyOneDate_ExpandsToOneMonth()
    {
        var useCase = UseCase(new InMemoryOwnerRevenueQueries());

        var fromOnly = await useCase.ExecuteAsync(OwnerA, new OwnerRevenueQuery(From: new DateOnly(2026, 10, 15)));
        var toOnly = await useCase.ExecuteAsync(OwnerA, new OwnerRevenueQuery(To: new DateOnly(2026, 10, 15)));

        Assert.Equal((new DateOnly(2026, 10, 15), new DateOnly(2026, 11, 15)), (fromOnly.From, fromOnly.To));
        Assert.Equal((new DateOnly(2026, 9, 15), new DateOnly(2026, 10, 15)), (toOnly.From, toOnly.To));
    }

    private static Settlement Settlement(int owner, DateOnly start, DateOnly end, SettlementStatus status, decimal commission, decimal payout,
        params (int Lot, decimal Commission)[] lines)
    {
        var settlement = new Settlement
        {
            OwnerProfileId = owner, PeriodStart = start, PeriodEnd = end, Status = status, CommissionRate = 0.10m,
            CommissionAmount = commission, NetPayout = payout,
        };
        foreach (var (lot, lineCommission) in lines)
            settlement.Lines.Add(new SettlementLine { ParkingLotId = lot, LineType = SettlementLineType.Booking, CommissionAmount = lineCommission });
        return settlement;
    }
}
