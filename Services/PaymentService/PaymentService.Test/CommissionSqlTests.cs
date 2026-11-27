using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features.Reports;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Persistence;
using PaymentService.Infrastructure.Persistence.Queries;
using Xunit.Abstractions;

namespace PaymentService.Test;

/// <summary>
/// US-079: truy vấn sổ hoa hồng dịch được sang SQL của PostgreSQL và lọc / GROUP BY ngay trong database; model có đúng hai index duy nhất
/// chống ghi trùng. Chỉ sinh SQL bằng provider Npgsql (ToQueryString) – không mở kết nối database.
/// </summary>
public class CommissionSqlTests(ITestOutputHelper output) : IDisposable
{
    private static readonly OwnerRevenueFilter Filter = new(7, null, new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1));
    private static readonly DateTime From = new(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc);

    private readonly PaymentDbContext _db = new(new DbContextOptionsBuilder<PaymentDbContext>()
        .UseNpgsql("Host=localhost;Database=pm_payment_sql_shape").Options);

    // ===== Job =====

    [Fact]
    public void AccrualCandidates_AntiJoinOnAccrual_OldestFirst_Limited()
    {
        var sql = Sql(CommissionQueryShapes.AccrualCandidates(_db.Payments, _db.CommissionEntries, [], 200));

        Assert.Contains("NOT EXISTS", sql);
        Assert.Contains("\"EntryType\" = 'Accrual'", sql);
        Assert.Contains("\"PaidAtUtc\" IS NOT NULL", sql);
        Assert.Contains("ORDER BY p.\"PaidAtUtc\", p.\"Id\"", sql);
        Assert.Contains("LIMIT", sql);
        Assert.DoesNotContain("\"Purpose\"", sql);                                  // không loại mục đích nào → không thêm điều kiện
    }

    [Fact]
    public void AccrualCandidates_ExcludedPurposes_FilteredInDatabase()
    {
        var sql = Sql(CommissionQueryShapes.AccrualCandidates(_db.Payments, _db.CommissionEntries, [PaymentPurpose.Overstay], 200));
        Assert.Contains("\"Purpose\"", sql);
    }

    [Fact]
    public void ReversalCandidates_CompareRefundedAmountWithCoveredReversal()
    {
        var sql = Sql(CommissionQueryShapes.ReversalCandidates(_db.Payments, _db.CommissionEntries, 200));

        Assert.Contains("INNER JOIN \"Payments\"", sql);
        Assert.Contains("\"RefundedAmount\" > 0", sql);
        Assert.Contains("\"EntryType\" = 'Reversal'", sql);
        Assert.Contains("\"RefundedAmountCovered\" >= ", sql);
        Assert.Contains("max(", sql);
        Assert.Contains("LIMIT", sql);
    }

    // ===== Báo cáo chủ bãi =====

    [Fact]
    public void OwnerCommission_ByLotAndDay_FilteredByOwnerAndPaidAt_GroupedInDatabase()
    {
        foreach (var sql in new[]
                 {
                     Sql(OwnerRevenueQueryShapes.SumCommissionByLot(_db.CommissionEntries, Filter)),
                     Sql(OwnerRevenueQueryShapes.SumCommissionByVietnamDay(_db.CommissionEntries, Filter)),
                     Sql(OwnerRevenueQueryShapes.SumCommissionByVietnamDayHour(_db.CommissionEntries, Filter)),
                 })
        {
            Assert.Contains("FROM \"CommissionEntries\"", sql);
            Assert.Contains("\"OwnerProfileId\" = ", sql);
            Assert.Contains("\"PaidAtUtc\" >= ", sql);
            Assert.Contains("GROUP BY", sql);
            Assert.Contains("sum(", sql);
        }
    }

    [Fact]
    public void OwnerPending_UsesPaymentsAndExistsSubqueries()
    {
        var sql = Sql(OwnerRevenueQueryShapes.CountCommissionPending(_db.Payments, _db.CommissionEntries, Filter, []));

        Assert.Contains("FROM \"Payments\"", sql);
        Assert.Contains("EXISTS", sql);
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("count(", sql);
    }

    [Fact]
    public void EntriesOfPayments_FiltersByPaymentIdsInDatabase()
    {
        var sql = Sql(OwnerRevenueQueryShapes.EntriesOfPayments(_db.CommissionEntries, [1, 2, 3]));
        Assert.Contains("\"PaymentId\" = ANY (", sql);
    }

    // ===== Admin =====

    [Fact]
    public void AdminQueries_TranslateAndAggregateInDatabase()
    {
        foreach (var sql in new[]
                 {
                     Sql(AdminCommissionQueryShapes.SumRevenueByLot(_db.Payments, From, To)),
                     Sql(AdminCommissionQueryShapes.SumCommissionByLotSource(_db.CommissionEntries, From, To)),
                     Sql(AdminCommissionQueryShapes.CountPendingByLotDay(_db.Payments, _db.CommissionEntries, From, To, [PaymentPurpose.WalkIn])),
                     Sql(AdminCommissionQueryShapes.SumRevenueByDay(_db.Payments, From, To)),
                     Sql(AdminCommissionQueryShapes.SumCommissionByDay(_db.CommissionEntries, From, To)),
                 })
        {
            Assert.Contains("GROUP BY", sql);
            Assert.Contains("\"PaidAtUtc\" >= ", sql);
            Assert.Contains("\"PaidAtUtc\" < ", sql);
            Assert.DoesNotContain("\"UserId\"", sql);                               // không có dữ liệu từng khách
        }
    }

    // ===== Model =====

    [Fact]
    public void Model_HasUniqueAccrualPerPayment_AndUniqueReversalPerCoveredAmount()
    {
        var entity = _db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(CommissionEntry))!;
        var unique = entity.GetIndexes().Where(i => i.IsUnique).ToDictionary(i => i.GetDatabaseName()!);

        Assert.Equal("CommissionEntries", entity.GetTableName());
        var accrual = unique["IX_CommissionEntries_PaymentId_Accrual"];
        Assert.Equal(["PaymentId"], accrual.Properties.Select(p => p.Name));
        Assert.Equal("\"EntryType\" = 'Accrual'", accrual.GetFilter());
        var reversal = unique["IX_CommissionEntries_PaymentId_RefundedAmountCovered_Reversal"];
        Assert.Equal(["PaymentId", "RefundedAmountCovered"], reversal.Properties.Select(p => p.Name));
        Assert.Equal("\"EntryType\" = 'Reversal'", reversal.GetFilter());
        Assert.Equal(2, unique.Count);
        Assert.Equal(6, entity.FindProperty(nameof(CommissionEntry.Rate))!.GetScale());
        Assert.Contains(entity.GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual(["OwnerProfileId", "PaidAtUtc"]));
        Assert.Equal(DeleteBehavior.Restrict, Assert.Single(entity.GetForeignKeys()).DeleteBehavior);
    }

    [Fact]
    public void PaymentEntity_IsUnchanged_NoCommissionColumnOrNavigation()
    {
        var payment = _db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Payment))!;
        Assert.DoesNotContain(payment.GetProperties(), p => p.Name.Contains("Commission"));
        Assert.DoesNotContain(payment.GetNavigations(), n => n.TargetEntityType.ClrType == typeof(CommissionEntry));
    }

    private string Sql<T>(IQueryable<T> query)
    {
        var sql = query.ToQueryString();
        output.WriteLine(sql);
        return sql;
    }

    public void Dispose() => _db.Dispose();
}
