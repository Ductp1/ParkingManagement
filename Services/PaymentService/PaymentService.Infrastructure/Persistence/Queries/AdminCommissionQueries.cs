using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features.Commission;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Queries;

/// <summary>US-079: hình dạng truy vấn tổng hợp hoa hồng toàn sàn (Admin) – dùng chung cho EF và test trong bộ nhớ.</summary>
public static class AdminCommissionQueryShapes
{
    private static IQueryable<Payment> Collected(IQueryable<Payment> payments, DateTime fromUtc, DateTime toUtc)
        => payments.WhereCollected().Where(p => p.PaidAtUtc >= fromUtc && p.PaidAtUtc < toUtc);

    private static IQueryable<CommissionEntry> InRange(IQueryable<CommissionEntry> entries, DateTime fromUtc, DateTime toUtc)
        => entries.Where(e => e.PaidAtUtc >= fromUtc && e.PaidAtUtc < toUtc);

    public static IQueryable<AdminRevenueLotRow> SumRevenueByLot(IQueryable<Payment> payments, DateTime fromUtc, DateTime toUtc)
        => Collected(payments, fromUtc, toUtc)
            .GroupBy(p => new { p.ParkingLotId, p.OwnerProfileId })
            .Select(g => new AdminRevenueLotRow(g.Key.ParkingLotId, g.Key.OwnerProfileId, g.Sum(p => p.Amount), g.Sum(p => p.RefundedAmount),
                g.Count()));

    public static IQueryable<AdminCommissionLotSourceRow> SumCommissionByLotSource(IQueryable<CommissionEntry> entries, DateTime fromUtc,
        DateTime toUtc)
        => InRange(entries, fromUtc, toUtc)
            .GroupBy(e => new { e.ParkingLotId, e.OwnerProfileId, e.RateSource })
            .Select(g => new AdminCommissionLotSourceRow(g.Key.ParkingLotId, g.Key.OwnerProfileId, g.Key.RateSource, g.Sum(e => e.Amount),
                g.Sum(e => e.EntryType == CommissionEntryType.Accrual ? e.BaseAmount : 0),
                -g.Sum(e => e.EntryType == CommissionEntryType.Reversal ? e.BaseAmount : 0),
                g.Count(e => e.EntryType == CommissionEntryType.Accrual)));

    public static IQueryable<AdminPendingLotDayRow> CountPendingByLotDay(IQueryable<Payment> payments, IQueryable<CommissionEntry> entries,
        DateTime fromUtc, DateTime toUtc, IReadOnlyCollection<PaymentPurpose> excludedPurposes)
        => Collected(payments, fromUtc, toUtc)
            .WherePendingCommission(entries, excludedPurposes)
            .Select(p => new { p.ParkingLotId, p.OwnerProfileId, Local = p.PaidAtUtc!.Value.AddHours(7) })
            .GroupBy(x => new { x.ParkingLotId, x.OwnerProfileId, x.Local.Year, x.Local.Month, x.Local.Day })
            .Select(g => new AdminPendingLotDayRow(g.Key.ParkingLotId, g.Key.OwnerProfileId, g.Key.Year, g.Key.Month, g.Key.Day, g.Count()));

    public static IQueryable<AdminRevenueDayRow> SumRevenueByDay(IQueryable<Payment> payments, DateTime fromUtc, DateTime toUtc)
        => Collected(payments, fromUtc, toUtc)
            .Select(p => new { Local = p.PaidAtUtc!.Value.AddHours(7), p.Amount, p.RefundedAmount })
            .GroupBy(x => new { x.Local.Year, x.Local.Month, x.Local.Day })
            .Select(g => new AdminRevenueDayRow(g.Key.Year, g.Key.Month, g.Key.Day, g.Sum(x => x.Amount), g.Sum(x => x.RefundedAmount), g.Count()));

    public static IQueryable<AdminCommissionDayRow> SumCommissionByDay(IQueryable<CommissionEntry> entries, DateTime fromUtc, DateTime toUtc)
        => InRange(entries, fromUtc, toUtc)
            .Select(e => new { Local = e.PaidAtUtc.AddHours(7), e.Amount })
            .GroupBy(x => new { x.Local.Year, x.Local.Month, x.Local.Day })
            .Select(g => new AdminCommissionDayRow(g.Key.Year, g.Key.Month, g.Key.Day, g.Sum(x => x.Amount)));
}

/// <summary>US-079: tổng hợp hoa hồng toàn sàn – chỉ đọc, AsNoTracking, GROUP BY ở database.</summary>
public sealed class AdminCommissionQueries(PaymentDbContext db) : IAdminCommissionQueries
{
    public async Task<IReadOnlyList<AdminRevenueLotRow>> SumRevenueByLotAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        => await AdminCommissionQueryShapes.SumRevenueByLot(db.Payments.AsNoTracking(), fromUtc, toUtc).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AdminCommissionLotSourceRow>> SumCommissionByLotSourceAsync(DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken)
        => await AdminCommissionQueryShapes.SumCommissionByLotSource(db.CommissionEntries.AsNoTracking(), fromUtc, toUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AdminPendingLotDayRow>> CountPendingByLotDayAsync(DateTime fromUtc, DateTime toUtc,
        IReadOnlyCollection<PaymentPurpose> excludedPurposes, CancellationToken cancellationToken)
        => await AdminCommissionQueryShapes.CountPendingByLotDay(db.Payments.AsNoTracking(), db.CommissionEntries.AsNoTracking(), fromUtc, toUtc,
            excludedPurposes).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AdminRevenueDayRow>> SumRevenueByDayAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        => await AdminCommissionQueryShapes.SumRevenueByDay(db.Payments.AsNoTracking(), fromUtc, toUtc).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AdminCommissionDayRow>> SumCommissionByDayAsync(DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken)
        => await AdminCommissionQueryShapes.SumCommissionByDay(db.CommissionEntries.AsNoTracking(), fromUtc, toUtc).ToListAsync(cancellationToken);
}
