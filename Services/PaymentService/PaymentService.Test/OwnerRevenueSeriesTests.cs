using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Reports;
using static PaymentService.Test.OwnerRevenueTestData;

namespace PaymentService.Test;

/// <summary>
/// US-078 (UC-35): chuỗi doanh thu theo giờ/ngày/tuần/tháng cho biểu đồ. Ngày/giờ theo lịch Việt Nam (+07:00),
/// from bao gồm, to loại trừ; đủ mọi điểm kể cả điểm bằng 0. Port chạy trong bộ nhớ bằng đúng hình dạng truy vấn của Infrastructure.
/// </summary>
public class OwnerRevenueSeriesTests
{
    private static readonly TimeSpan Ict = TimeSpan.FromHours(7);

    private static GetOwnerRevenueSeriesUseCase UseCase(IOwnerRevenueQueries queries)
        => new(queries, new FixedTimeProvider(Utc(2026, 10, 15, 3)));

    private static OwnerRevenueQuery Range(int fromMonth, int fromDay, int toMonth, int toDay, int? lot = null)
        => new(new DateOnly(2026, fromMonth, fromDay), new DateOnly(2026, toMonth, toDay), lot);

    private static DateTimeOffset At(int month, int day, int hour = 0) => new(2026, month, day, hour, 0, 0, Ict);

    // ----- Day -----

    [Fact]
    public async Task Day_IsDefault_BucketsByVietnamDay_AndFillsEmptyDays()
    {
        var queries = new InMemoryOwnerRevenueQueries(
        [
            Paid(OwnerA, LotA1, 10_000, Utc(2026, 9, 30, 17)),          // 01/10 00:00 ICT
            Paid(OwnerA, LotA1, 20_000, Utc(2026, 10, 1, 16, 59, 59)),  // 01/10 23:59:59 ICT
            Paid(OwnerA, LotA1, 40_000, Utc(2026, 10, 1, 17)),          // 02/10 00:00 ICT
        ]);

        var series = await UseCase(queries).ExecuteAsync(OwnerA, Range(10, 1, 10, 4), null);

        Assert.Equal("Day", series.Granularity);
        Assert.Equal(
        [
            new OwnerRevenuePointDto(At(10, 1), At(10, 2), 30_000, 0, 30_000, 2),
            new OwnerRevenuePointDto(At(10, 2), At(10, 3), 40_000, 0, 40_000, 1),
            new OwnerRevenuePointDto(At(10, 3), At(10, 4), 0, 0, 0, 0),
        ], series.Points);
        Assert.Equal(TimeSpan.FromHours(7), series.Points[0].PeriodStart.Offset);
        Assert.Equal("OriginalPaymentPeriod", series.RefundBasis);
    }

    [Fact]
    public async Task Day_PartialRefund_ReducesNetOfOriginalDay()
    {
        var queries = new InMemoryOwnerRevenueQueries(
            [Paid(OwnerA, LotA1, 100_000, Utc(2026, 10, 1, 2), status: PaymentStatus.PartiallyRefunded, refunded: 25_000)]);

        var series = await UseCase(queries).ExecuteAsync(OwnerA, Range(10, 1, 10, 2), "day");

        Assert.Equal(new OwnerRevenuePointDto(At(10, 1), At(10, 2), 100_000, 25_000, 75_000, 1), Assert.Single(series.Points));
    }

    // ----- Week / Month -----

    [Fact]
    public async Task Week_StartsOnMonday_AndEdgeWeeksAreClippedToRange()
    {
        // 01/10/2026 là thứ Năm; 05/10, 12/10, 19/10 là thứ Hai.
        var queries = new InMemoryOwnerRevenueQueries(
        [
            Paid(OwnerA, LotA1, 1_000, Utc(2026, 10, 4, 16, 59)),   // CN 04/10 23:59 ICT → tuần 1
            Paid(OwnerA, LotA1, 2_000, Utc(2026, 10, 4, 17)),       // T2 05/10 00:00 ICT → tuần 2
            Paid(OwnerA, LotA1, 4_000, Utc(2026, 10, 18, 3)),       // CN 18/10 → tuần 3
            Paid(OwnerA, LotA1, 8_000, Utc(2026, 10, 19, 3)),       // T2 19/10 → tuần 4 (chỉ 1 ngày)
            Paid(OwnerA, LotA1, 16_000, Utc(2026, 10, 20, 3)),      // 20/10 = to → ngoài
        ]);

        var series = await UseCase(queries).ExecuteAsync(OwnerA, Range(10, 1, 10, 20), "Week");

        Assert.Equal(
        [
            new OwnerRevenuePointDto(At(10, 1), At(10, 5), 1_000, 0, 1_000, 1),
            new OwnerRevenuePointDto(At(10, 5), At(10, 12), 2_000, 0, 2_000, 1),
            new OwnerRevenuePointDto(At(10, 12), At(10, 19), 4_000, 0, 4_000, 1),
            new OwnerRevenuePointDto(At(10, 19), At(10, 20), 8_000, 0, 8_000, 1),
        ], series.Points);
    }

    [Fact]
    public async Task Month_BucketsByCalendarMonth_AndEdgeMonthsAreClipped()
    {
        var queries = new InMemoryOwnerRevenueQueries(
        [
            Paid(OwnerA, LotA1, 1_000, Utc(2026, 9, 20, 3)),
            Paid(OwnerA, LotA1, 2_000, Utc(2026, 9, 30, 17)),       // 01/10 00:00 ICT → tháng 10
            Paid(OwnerA, LotA1, 4_000, Utc(2026, 10, 31, 17)),      // 01/11 00:00 ICT → tháng 11
        ]);

        var series = await UseCase(queries).ExecuteAsync(OwnerA, Range(9, 15, 11, 10), "MONTH");

        Assert.Equal(
        [
            new OwnerRevenuePointDto(At(9, 15), At(10, 1), 1_000, 0, 1_000, 1),
            new OwnerRevenuePointDto(At(10, 1), At(11, 1), 2_000, 0, 2_000, 1),
            new OwnerRevenuePointDto(At(11, 1), At(11, 10), 4_000, 0, 4_000, 1),
        ], series.Points);
    }

    [Fact]
    public async Task SumOfPoints_EqualsSummaryTotals()
    {
        var payments = new[]
        {
            Paid(OwnerA, LotA1, 30_000, Utc(2026, 10, 2, 3)),
            Paid(OwnerA, LotA2, 50_000, Utc(2026, 10, 9, 3), status: PaymentStatus.PartiallyRefunded, refunded: 10_000),
            Paid(OwnerA, LotA1, 70_000, Utc(2026, 10, 30, 3)),
        };
        var query = Range(10, 1, 11, 1);

        var series = await UseCase(new InMemoryOwnerRevenueQueries(payments)).ExecuteAsync(OwnerA, query, "Week");
        var summary = await new GetOwnerRevenueSummaryUseCase(new InMemoryOwnerRevenueQueries(payments), new FixedTimeProvider(Utc(2026, 10, 15)))
            .ExecuteAsync(OwnerA, query);

        Assert.Equal(summary.Totals.GrossRevenue, series.Points.Sum(p => p.GrossRevenue));
        Assert.Equal(summary.Totals.NetRevenue, series.Points.Sum(p => p.NetRevenue));
        Assert.Equal(summary.Totals.TransactionCount, series.Points.Sum(p => p.TransactionCount));
    }

    // ----- Hour -----

    [Fact]
    public async Task Hour_Has24PointsPerVietnamDay()
    {
        var queries = new InMemoryOwnerRevenueQueries(
        [
            Paid(OwnerA, LotA1, 30_000, Utc(2026, 10, 5, 1, 30)),   // 05/10 08:30 ICT
            Paid(OwnerA, LotA1, 20_000, Utc(2026, 10, 5, 1, 59)),   // 05/10 08:59 ICT
            Paid(OwnerA, LotA1, 10_000, Utc(2026, 10, 4, 17, 5)),   // 05/10 00:05 ICT
            Paid(OwnerA, LotA1, 99_000, Utc(2026, 10, 5, 17)),      // 06/10 00:00 ICT → ngoài
        ]);

        var series = await UseCase(queries).ExecuteAsync(OwnerA, Range(10, 5, 10, 6), "hour");

        Assert.Equal(24, series.Points.Count);
        Assert.Equal(new OwnerRevenuePointDto(At(10, 5, 0), At(10, 5, 1), 10_000, 0, 10_000, 1), series.Points[0]);
        Assert.Equal(new OwnerRevenuePointDto(At(10, 5, 8), At(10, 5, 9), 50_000, 0, 50_000, 2), series.Points[8]);
        Assert.Equal(At(10, 6, 0), series.Points[^1].PeriodEnd);
        Assert.Equal(60_000, series.Points.Sum(p => p.GrossRevenue));
    }

    [Fact]
    public async Task Hour_SevenDays_IsAllowed()
    {
        var series = await UseCase(new InMemoryOwnerRevenueQueries()).ExecuteAsync(OwnerA, Range(10, 1, 10, 8), "Hour");
        Assert.Equal(7 * 24, series.Points.Count);
    }

    [Fact]
    public async Task Hour_MoreThanSevenDays_Throws400()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            UseCase(new InMemoryOwnerRevenueQueries()).ExecuteAsync(OwnerA, Range(10, 1, 10, 9), "Hour"));
        Assert.Equal("granularity=Hour chỉ áp dụng cho khoảng tối đa 7 ngày; hãy truyền from/to ngắn hơn.", ex.Message);
    }

    // ----- Phân quyền / tham số -----

    [Fact]
    public async Task OnlyOwnData_LotOfAnotherOwner_GivesZeroPoints()
    {
        var queries = new InMemoryOwnerRevenueQueries(
            [Paid(OwnerB, LotB1, 70_000, Utc(2026, 10, 2, 3)), Paid(OwnerA, LotA1, 10_000, Utc(2026, 10, 2, 3))]);

        var own = await UseCase(queries).ExecuteAsync(OwnerA, Range(10, 1, 10, 4), "Day");
        var foreignLot = await UseCase(queries).ExecuteAsync(OwnerA, Range(10, 1, 10, 4, LotB1), "Day");

        Assert.Equal(10_000, own.Points.Sum(p => p.GrossRevenue));
        Assert.Equal(3, foreignLot.Points.Count);
        Assert.All(foreignLot.Points, p => Assert.Equal(0, p.TransactionCount));
        Assert.Equal((OwnerA, LotB1), (queries.LastFilter!.OwnerProfileId, queries.LastFilter.ParkingLotId));
    }

    [Theory]
    [InlineData("Year")]
    [InlineData("1")]
    [InlineData("-1")]
    [InlineData("Day,Week")]
    public async Task InvalidGranularity_Throws400(string granularity)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            UseCase(new InMemoryOwnerRevenueQueries()).ExecuteAsync(OwnerA, Range(10, 1, 10, 4), granularity));
        Assert.Equal("granularity phải là Hour, Day, Week hoặc Month.", ex.Message);
    }

    [Fact]
    public async Task InvalidRange_Throws400_BeforeQuerying()
    {
        var queries = new InMemoryOwnerRevenueQueries();
        await Assert.ThrowsAsync<ValidationException>(() => UseCase(queries).ExecuteAsync(OwnerA, Range(10, 4, 10, 1), "Day"));
        Assert.Null(queries.LastFilter);
    }

    [Fact]
    public async Task OwnerWithoutProfile_Throws403()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            UseCase(new InMemoryOwnerRevenueQueries()).ExecuteAsync(null, Range(10, 1, 10, 4), "Day"));
    }
}
