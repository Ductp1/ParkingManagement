using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Persistence;
using PaymentService.Infrastructure.Persistence.Queries;

namespace PaymentService.Test;

// US-080: index duy nhất chống ghi trùng (job chạy lặp / 2 instance) và SQL của truy vấn sinh kỳ (ToQueryString – không mở database).
public sealed class SettlementPersistenceTests : IDisposable
{
    private static readonly DateTime Cutoff = new(2026, 11, 8, 17, 0, 0, DateTimeKind.Utc);
    private readonly PaymentDbContext _db = new(new DbContextOptionsBuilder<PaymentDbContext>()
        .UseNpgsql("Host=localhost;Database=pm_payment_sql_shape").Options);

    public void Dispose() => _db.Dispose();

    private Microsoft.EntityFrameworkCore.Metadata.IIndex Index<T>(string name)
        => _db.Model.FindEntityType(typeof(T))!.GetIndexes().Single(i => i.GetDatabaseName() == name);

    [Fact]
    public void UniqueIndexes_EachPaymentRefundHoldAndVoucherOnlyOnce()
    {
        var payment = Index<SettlementLine>("UX_SettlementLines_Payment");
        Assert.True(payment.IsUnique);
        Assert.Equal("\"PaymentId\" IS NOT NULL AND \"LineType\" IN ('Booking', 'WalkIn')", payment.GetFilter());

        var refund = Index<SettlementLine>("UX_SettlementLines_Refund");
        Assert.True(refund.IsUnique);

        Assert.True(Index<SettlementLine>("UX_SettlementLines_Hold").IsUnique);
        Assert.Contains("\"Amount\" < 0", Index<SettlementLine>("UX_SettlementLines_Hold").GetFilter());
        Assert.Contains("\"Amount\" > 0", Index<SettlementLine>("UX_SettlementLines_HoldRelease").GetFilter());

        var voucher = Index<FinancialAdjustment>("UX_FinancialAdjustments_Voucher");
        Assert.True(voucher.IsUnique);
        Assert.Equal("\"SourceType\" = 'CompensationVoucher'", voucher.GetFilter());

        Assert.True(Index<Settlement>("IX_Settlements_OwnerProfileId_PeriodStart").IsUnique);
        Assert.True(Index<Settlement>("IX_Settlements_Code").IsUnique);
        Assert.True(Index<Settlement>("IX_Settlements_CarryOverFromSettlementId").IsUnique);
        Assert.True(Index<SettlementPeriod>("IX_SettlementPeriods_PeriodStart").IsUnique);
    }

    [Fact]
    public void RowVersion_IsConcurrencyToken_OnSettlementAndAdjustment()
    {
        Assert.True(_db.Model.FindEntityType(typeof(Settlement))!.FindProperty(nameof(Settlement.RowVersion))!.IsConcurrencyToken);
        Assert.True(_db.Model.FindEntityType(typeof(FinancialAdjustment))!.FindProperty(nameof(FinancialAdjustment.RowVersion))!.IsConcurrencyToken);
        Assert.Null(_db.Model.FindEntityType(typeof(SettlementPeriod))!.FindProperty(nameof(SettlementPeriod.CutoffUtc)));
    }

    [Fact]
    public void Sql_UnsettledPayments_FiltersOwnerStatusCutoffAndExistingLinesInDatabase()
    {
        var sql = SettlementQueryShapes.UnsettledPayments(_db.Payments, _db.SettlementLines, 1, Cutoff, 42).ToQueryString();

        Assert.Contains("\"OwnerProfileId\" =", sql);
        Assert.Contains("'Succeeded'", sql);
        Assert.Contains("\"PaidAtUtc\" <", sql);
        Assert.Contains("NOT EXISTS", sql);
        Assert.Contains("\"SettlementId\" <>", sql);                            // dòng của kỳ đang tính lại coi như chưa có
        Assert.DoesNotContain("\"UserId\"", sql);                               // không đọc dữ liệu khách
        Assert.DoesNotContain("\"RawCallbackJson\"", sql);
    }

    [Fact]
    public void Sql_HeldElsewhere_GroupsByComplaintInDatabase()
    {
        var sql = SettlementQueryShapes.HeldElsewhere(_db.SettlementLines, _db.Settlements, 1, null).ToQueryString();
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("'DisputeHold'", sql);
        Assert.Contains("sum(", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sql_PriorRefunds_SumsInDatabase()
    {
        var sql = SettlementQueryShapes.PriorRefunds(_db.SettlementLines, [1, 2], 5).ToQueryString();
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("'Refund'", sql);
    }

    [Fact]
    public void Sql_CandidateOwners_AllTranslate()
    {
        Assert.Contains("DISTINCT", SettlementQueryShapes.OwnersWithUnsettledPayments(_db.Payments, _db.SettlementLines, Cutoff).ToQueryString());
        Assert.Contains("JOIN", SettlementQueryShapes.OwnersWithUnsettledRefunds(_db.Refunds, _db.Payments, _db.SettlementLines, Cutoff).ToQueryString());
        Assert.Contains("'Approved'", SettlementQueryShapes.OwnersWithApprovedAdjustments(_db.FinancialAdjustments).ToQueryString());
        Assert.Contains(":REVOKED", SettlementQueryShapes.OwnersWithVouchersToCharge(_db.CompensationVouchers, _db.FinancialAdjustments, Cutoff)
            .ToQueryString());
        Assert.Contains("\"NetPayout\" < 0", SettlementQueryShapes.OwnersWithCarryOver(_db.Settlements).ToQueryString());
        Assert.Contains("HAVING", SettlementQueryShapes.OwnersWithOpenHolds(_db.SettlementLines, _db.Settlements).ToQueryString());
    }

    [Fact]
    public void Sql_RefundsAttachableAdjustmentsCarryOver_Translate()
    {
        Assert.Contains("'Completed'", SettlementQueryShapes.UnsettledRefunds(_db.Refunds, _db.Payments, _db.SettlementLines, 1, Cutoff, null)
            .ToQueryString());
        Assert.Contains("\"ApprovedAtUtc\" <=", SettlementQueryShapes.AttachableAdjustments(_db.FinancialAdjustments, 1, Cutoff, 3).ToQueryString());
        Assert.Contains("ORDER BY", SettlementQueryShapes.CarryOver(_db.Settlements, 1, null).ToQueryString());
        Assert.Contains("'Accrual'", SettlementQueryShapes.Accruals(_db.CommissionEntries, [1]).ToQueryString());
    }
}
