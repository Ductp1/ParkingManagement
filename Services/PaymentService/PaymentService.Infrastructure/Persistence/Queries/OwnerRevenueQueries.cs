using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features.Reports;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Queries;

/// <summary>
/// US-078: hình dạng truy vấn báo cáo doanh thu chủ bãi (lọc + GROUP BY + SUM). Tách khỏi DbContext để
/// EF dịch sang SQL (dùng index Payments(OwnerProfileId, Status, PaidAtUtc)) và test chạy cùng logic trong bộ nhớ.
/// </summary>
public static class OwnerRevenueQueryShapes
{
    public static IQueryable<OwnerRevenueGroupRow> SumByLotMethodPurpose(IQueryable<Payment> payments, OwnerRevenueFilter filter)
        => payments.WhereRevenue(filter)
            .GroupBy(p => new { p.ParkingLotId, p.Method, p.Purpose })
            .Select(g => new OwnerRevenueGroupRow(g.Key.ParkingLotId, g.Key.Method, g.Key.Purpose,
                g.Sum(p => p.Amount), g.Sum(p => p.RefundedAmount), g.Count()));

    /// <summary>Giờ trong ngày theo giờ Việt Nam = giờ của (PaidAtUtc + 7h).</summary>
    public static IQueryable<OwnerRevenueHourRow> SumByHourOfDay(IQueryable<Payment> payments, OwnerRevenueFilter filter)
        => payments.WhereRevenue(filter)
            .GroupBy(p => p.PaidAtUtc!.Value.AddHours(7).Hour)
            .Select(g => new OwnerRevenueHourRow(g.Key, g.Sum(p => p.Amount), g.Sum(p => p.RefundedAmount), g.Count()));

    public static IQueryable<OwnerRefundReasonRow> SumCompletedRefundsByReason(IQueryable<Payment> payments, OwnerRevenueFilter filter)
        => payments.WhereRevenue(filter)
            .SelectMany(p => p.Refunds)
            .Where(r => r.Status == RefundStatus.Completed)
            .GroupBy(r => r.Reason)
            .Select(g => new OwnerRefundReasonRow(g.Key, g.Count(), g.Sum(r => r.Amount)));

    /// <summary>Kỳ Approved/Paid nằm trọn trong [From, To). Lọc theo bãi → hoa hồng chỉ cộng các dòng của bãi đó.</summary>
    public static IQueryable<OwnerSettlementRow> SettledPeriods(IQueryable<Settlement> settlements, OwnerRevenueFilter filter)
    {
        var (ownerProfileId, from, to) = (filter.OwnerProfileId, filter.From, filter.To);
        var settled = settlements.Where(s => s.OwnerProfileId == ownerProfileId
            && (s.Status == SettlementStatus.Approved || s.Status == SettlementStatus.Paid)
            && s.PeriodStart >= from && s.PeriodEnd < to);

        if (filter.ParkingLotId is { } lotId)
            return settled.Select(s => new OwnerSettlementRow(s.PeriodStart, s.PeriodEnd,
                s.Lines.Where(l => l.ParkingLotId == lotId).Sum(l => l.CommissionAmount), s.NetPayout));
        return settled.Select(s => new OwnerSettlementRow(s.PeriodStart, s.PeriodEnd, s.CommissionAmount, s.NetPayout));
    }
}

/// <summary>Kho truy vấn báo cáo doanh thu chủ bãi (US-078) – chỉ đọc, AsNoTracking, tổng hợp ở database.</summary>
public sealed class OwnerRevenueQueries(PaymentDbContext db) : IOwnerRevenueQueries
{
    public async Task<IReadOnlyList<OwnerRevenueGroupRow>> SumByLotMethodPurposeAsync(OwnerRevenueFilter filter,
        CancellationToken cancellationToken)
        => await OwnerRevenueQueryShapes.SumByLotMethodPurpose(db.Payments.AsNoTracking(), filter).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OwnerRevenueHourRow>> SumByHourOfDayAsync(OwnerRevenueFilter filter, CancellationToken cancellationToken)
        => await OwnerRevenueQueryShapes.SumByHourOfDay(db.Payments.AsNoTracking(), filter).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OwnerRefundReasonRow>> SumCompletedRefundsByReasonAsync(OwnerRevenueFilter filter,
        CancellationToken cancellationToken)
        => await OwnerRevenueQueryShapes.SumCompletedRefundsByReason(db.Payments.AsNoTracking(), filter).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OwnerSettlementRow>> ListSettledPeriodsAsync(OwnerRevenueFilter filter, CancellationToken cancellationToken)
        => await OwnerRevenueQueryShapes.SettledPeriods(db.Settlements.AsNoTracking(), filter).ToListAsync(cancellationToken);
}
