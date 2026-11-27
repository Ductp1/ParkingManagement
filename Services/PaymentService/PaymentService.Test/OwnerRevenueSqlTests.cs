using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Features.Reports;
using PaymentService.Infrastructure.Persistence;
using PaymentService.Infrastructure.Persistence.Queries;
using Xunit.Abstractions;

namespace PaymentService.Test;

/// <summary>
/// US-078: truy vấn báo cáo phải lọc + GROUP BY + SUM ngay trong PostgreSQL (không tải hết giao dịch về rồi cộng trong bộ nhớ).
/// Chỉ sinh SQL bằng provider Npgsql (ToQueryString) – không mở kết nối database.
/// </summary>
public class OwnerRevenueSqlTests(ITestOutputHelper output) : IDisposable
{
    private static readonly OwnerRevenueFilter Filter = new(7, null, new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1));

    private readonly PaymentDbContext _db = new(new DbContextOptionsBuilder<PaymentDbContext>()
        .UseNpgsql("Host=localhost;Database=pm_payment_sql_shape").Options);

    [Fact]
    public void ByLotMethodPurpose_GroupsAndSumsInDatabase_FilteredByOwnerStatusAndPaidAt()
    {
        var sql = Sql(OwnerRevenueQueryShapes.SumByLotMethodPurpose(_db.Payments, Filter));

        Assert.Contains("GROUP BY", sql);
        Assert.Contains("sum(", sql);
        Assert.Contains("count(", sql);
        Assert.Contains("\"OwnerProfileId\" = ", sql);
        Assert.Contains("\"PaidAtUtc\" >= ", sql);
        Assert.Contains("\"PaidAtUtc\" < ", sql);
        Assert.DoesNotContain("\"ParkingLotId\" = ", sql);
    }

    [Fact]
    public void LotFilter_IsAddedOnTopOfOwnerFilter()
    {
        var sql = Sql(OwnerRevenueQueryShapes.SumByLotMethodPurpose(_db.Payments, Filter with { ParkingLotId = 21 }));

        Assert.Contains("\"OwnerProfileId\" = ", sql);
        Assert.Contains("\"ParkingLotId\" = ", sql);
    }

    [Fact]
    public void ByHourOfDay_ComputesVietnamHourInDatabase()
    {
        var sql = Sql(OwnerRevenueQueryShapes.SumByHourOfDay(_db.Payments, Filter));

        Assert.Contains("GROUP BY", sql);
        Assert.Contains("date_part('hour'", sql);
        Assert.Contains("7", sql);
    }

    [Fact]
    public void ByVietnamDay_GroupsByLocalYearMonthDayInDatabase()
    {
        var sql = Sql(OwnerRevenueQueryShapes.SumByVietnamDay(_db.Payments, Filter));

        Assert.Contains("GROUP BY", sql);
        Assert.Contains("INTERVAL '7 hours'", sql);
        Assert.Contains("date_part('year'", sql);
        Assert.Contains("date_part('month'", sql);
        Assert.Contains("date_part('day'", sql);
        Assert.Contains("\"OwnerProfileId\" = ", sql);
    }

    [Fact]
    public void ByVietnamDayHour_AlsoGroupsByLocalHour()
    {
        var sql = Sql(OwnerRevenueQueryShapes.SumByVietnamDayHour(_db.Payments, Filter with { ParkingLotId = 21 }));

        Assert.Contains("GROUP BY", sql);
        Assert.Contains("date_part('hour'", sql);
        Assert.Contains("\"ParkingLotId\" = ", sql);
    }

    [Fact]
    public void Transactions_FilterSortPageInDatabase_AndReadNoDriverColumns()
    {
        var filter = new OwnerRevenueTransactionFilter(Filter with { ParkingLotId = 21 }, ParkingManagement.SharedKernel.Enums.PaymentMethod.Cash);
        var sql = Sql(OwnerRevenueQueryShapes.ToTransactionRows(
            OwnerRevenueQueryShapes.FilterTransactions(_db.Payments, filter).Skip(20).Take(20)));

        Assert.Contains("\"OwnerProfileId\" = ", sql);
        Assert.Contains("\"ParkingLotId\" = ", sql);
        Assert.Contains("\"Method\" = ", sql);
        Assert.Contains("ORDER BY p.\"PaidAtUtc\" DESC, p.\"Id\" DESC", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
        foreach (var column in new[] { "UserId", "TransferContent", "RawCallbackJson", "ConfirmedByStaffId" })
            Assert.DoesNotContain(column, sql);
    }

    [Fact]
    public void RefundsByReason_And_SettledPeriods_AreTranslatable()
    {
        var refundSql = Sql(OwnerRevenueQueryShapes.SumCompletedRefundsByReason(_db.Payments, Filter));
        Assert.Contains("\"Refunds\"", refundSql);
        Assert.Contains("GROUP BY", refundSql);

        var settlementSql = Sql(OwnerRevenueQueryShapes.SettledPeriods(_db.Settlements, Filter with { ParkingLotId = 21 }));
        Assert.Contains("\"SettlementLines\"", settlementSql);
        Assert.Contains("\"PeriodEnd\" < ", settlementSql);
    }

    private string Sql<T>(IQueryable<T> query)
    {
        var sql = query.ToQueryString();
        output.WriteLine(sql);
        return sql;
    }

    public void Dispose() => _db.Dispose();
}
