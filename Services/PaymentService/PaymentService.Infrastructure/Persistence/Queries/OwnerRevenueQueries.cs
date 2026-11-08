using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Contracts;
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

    /// <summary>Ngày theo lịch Việt Nam = năm/tháng/ngày của (PaidAtUtc + 7h).</summary>
    public static IQueryable<OwnerRevenueDayRow> SumByVietnamDay(IQueryable<Payment> payments, OwnerRevenueFilter filter)
        => payments.WhereRevenue(filter)
            .Select(p => new { Local = p.PaidAtUtc!.Value.AddHours(7), p.Amount, p.RefundedAmount })
            .GroupBy(x => new { x.Local.Year, x.Local.Month, x.Local.Day })
            .Select(g => new OwnerRevenueDayRow(g.Key.Year, g.Key.Month, g.Key.Day,
                g.Sum(x => x.Amount), g.Sum(x => x.RefundedAmount), g.Count()));

    public static IQueryable<OwnerRevenueDayHourRow> SumByVietnamDayHour(IQueryable<Payment> payments, OwnerRevenueFilter filter)
        => payments.WhereRevenue(filter)
            .Select(p => new { Local = p.PaidAtUtc!.Value.AddHours(7), p.Amount, p.RefundedAmount })
            .GroupBy(x => new { x.Local.Year, x.Local.Month, x.Local.Day, x.Local.Hour })
            .Select(g => new OwnerRevenueDayHourRow(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour,
                g.Sum(x => x.Amount), g.Sum(x => x.RefundedAmount), g.Count()));

    /// <summary>Giao dịch được tính doanh thu theo bộ lọc, mới nhất trước (sắp xếp trước khi chiếu để EF dịch được ORDER BY).</summary>
    public static IQueryable<Payment> FilterTransactions(IQueryable<Payment> payments, OwnerRevenueTransactionFilter filter)
    {
        var query = payments.WhereRevenue(filter.Range);
        if (filter.Method is { } method) query = query.Where(p => p.Method == method);
        if (filter.Purpose is { } purpose) query = query.Where(p => p.Purpose == purpose);
        if (filter.Status is { } status) query = query.Where(p => p.Status == status);
        return query.OrderByDescending(p => p.PaidAtUtc).ThenByDescending(p => p.Id);
    }

    /// <summary>Chỉ đọc các cột cần cho báo cáo – không đọc UserId, TransferContent, RawCallbackJson, ConfirmedByStaffId.</summary>
    public static IQueryable<OwnerRevenueTransactionRow> ToTransactionRows(IQueryable<Payment> payments)
        => payments.Select(p => new OwnerRevenueTransactionRow(p.Id, p.Code, p.PaidAtUtc!.Value, p.ParkingLotId, p.BookingCode,
            p.Method, p.Purpose, p.Status, p.Amount, p.RefundedAmount, p.ProviderTransactionId));

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

    // ===== US-079: hoa hồng (sổ CommissionEntries) =====

    /// <summary>Bút toán của chủ bãi trong khoảng lọc – theo PaidAtUtc của giao dịch gốc (khớp refundBasis), dùng index (OwnerProfileId, PaidAtUtc).</summary>
    public static IQueryable<CommissionEntry> WhereOwnerCommission(this IQueryable<CommissionEntry> entries, OwnerRevenueFilter filter)
    {
        var (ownerProfileId, fromUtc, toUtc) = (filter.OwnerProfileId, filter.FromUtc, filter.ToUtc);
        var query = entries.Where(e => e.OwnerProfileId == ownerProfileId && e.PaidAtUtc >= fromUtc && e.PaidAtUtc < toUtc);
        if (filter.ParkingLotId is { } lotId)
            query = query.Where(e => e.ParkingLotId == lotId);
        return query;
    }

    public static IQueryable<OwnerCommissionLotRow> SumCommissionByLot(IQueryable<CommissionEntry> entries, OwnerRevenueFilter filter)
        => entries.WhereOwnerCommission(filter)
            .GroupBy(e => e.ParkingLotId)
            .Select(g => new OwnerCommissionLotRow(g.Key, g.Sum(e => e.Amount)));

    public static IQueryable<OwnerCommissionDayRow> SumCommissionByVietnamDay(IQueryable<CommissionEntry> entries, OwnerRevenueFilter filter)
        => entries.WhereOwnerCommission(filter)
            .Select(e => new { Local = e.PaidAtUtc.AddHours(7), e.Amount })
            .GroupBy(x => new { x.Local.Year, x.Local.Month, x.Local.Day })
            .Select(g => new OwnerCommissionDayRow(g.Key.Year, g.Key.Month, g.Key.Day, g.Sum(x => x.Amount)));

    public static IQueryable<OwnerCommissionDayHourRow> SumCommissionByVietnamDayHour(IQueryable<CommissionEntry> entries,
        OwnerRevenueFilter filter)
        => entries.WhereOwnerCommission(filter)
            .Select(e => new { Local = e.PaidAtUtc.AddHours(7), e.Amount })
            .GroupBy(x => new { x.Local.Year, x.Local.Month, x.Local.Day, x.Local.Hour })
            .Select(g => new OwnerCommissionDayHourRow(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, g.Sum(x => x.Amount)));

    /// <summary>Giao dịch được tính doanh thu mà còn chờ tính hoa hồng, theo (bãi, ngày, giờ) Việt Nam.</summary>
    public static IQueryable<OwnerCommissionPendingRow> CountCommissionPending(IQueryable<Payment> payments, IQueryable<CommissionEntry> entries,
        OwnerRevenueFilter filter, IReadOnlyCollection<PaymentPurpose> excludedPurposes)
        => payments.WhereRevenue(filter)
            .WherePendingCommission(entries, excludedPurposes)
            .Select(p => new { p.ParkingLotId, Local = p.PaidAtUtc!.Value.AddHours(7) })
            .GroupBy(x => new { x.ParkingLotId, x.Local.Year, x.Local.Month, x.Local.Day, x.Local.Hour })
            .Select(g => new OwnerCommissionPendingRow(g.Key.ParkingLotId, g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, g.Count()));

    /// <summary>Các cột tối thiểu của bút toán thuộc những giao dịch đang hiển thị (≤ 1 trang / 10.000 dòng CSV).</summary>
    public static IQueryable<CommissionEntryLine> EntriesOfPayments(IQueryable<CommissionEntry> entries, IReadOnlyCollection<int> paymentIds)
    {
        var ids = paymentIds.ToList();      // List.Contains – tránh overload Span của array.Contains trong expression tree
        return entries.Where(e => ids.Contains(e.PaymentId))
            .Select(e => new CommissionEntryLine(e.PaymentId, e.EntryType, e.Amount, e.Rate, e.RateSource, e.RefundedAmountCovered));
    }

    /// <summary>Gộp bút toán của từng giao dịch (trong bộ nhớ – vài dòng mỗi giao dịch).</summary>
    public static IReadOnlyList<PaymentCommissionRow> ToPaymentCommission(IEnumerable<CommissionEntryLine> lines)
        => [.. lines.GroupBy(l => l.PaymentId).Select(g =>
        {
            var accrual = g.FirstOrDefault(l => l.EntryType == CommissionEntryType.Accrual);
            var covered = g.Where(l => l.EntryType == CommissionEntryType.Reversal).Select(l => l.RefundedAmountCovered).DefaultIfEmpty(0).Max();
            return new PaymentCommissionRow(g.Key, accrual is not null, g.Sum(l => l.Amount), accrual?.Rate, accrual?.RateSource, covered);
        })];
}

/// <summary>Một bút toán rút gọn (đọc để tính hoa hồng từng giao dịch).</summary>
public sealed record CommissionEntryLine(int PaymentId, CommissionEntryType EntryType, decimal Amount, decimal Rate, string RateSource,
    decimal RefundedAmountCovered);

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

    public async Task<IReadOnlyList<OwnerCommissionLotRow>> SumCommissionByLotAsync(OwnerRevenueFilter filter, CancellationToken cancellationToken)
        => await OwnerRevenueQueryShapes.SumCommissionByLot(db.CommissionEntries.AsNoTracking(), filter).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OwnerCommissionDayRow>> SumCommissionByVietnamDayAsync(OwnerRevenueFilter filter,
        CancellationToken cancellationToken)
        => await OwnerRevenueQueryShapes.SumCommissionByVietnamDay(db.CommissionEntries.AsNoTracking(), filter).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OwnerCommissionDayHourRow>> SumCommissionByVietnamDayHourAsync(OwnerRevenueFilter filter,
        CancellationToken cancellationToken)
        => await OwnerRevenueQueryShapes.SumCommissionByVietnamDayHour(db.CommissionEntries.AsNoTracking(), filter)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OwnerCommissionPendingRow>> CountCommissionPendingAsync(OwnerRevenueFilter filter,
        IReadOnlyCollection<PaymentPurpose> excludedPurposes, CancellationToken cancellationToken)
        => await OwnerRevenueQueryShapes.CountCommissionPending(db.Payments.AsNoTracking(), db.CommissionEntries.AsNoTracking(), filter,
            excludedPurposes).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PaymentCommissionRow>> ListCommissionByPaymentAsync(IReadOnlyCollection<int> paymentIds,
        CancellationToken cancellationToken)
        => OwnerRevenueQueryShapes.ToPaymentCommission(
            await OwnerRevenueQueryShapes.EntriesOfPayments(db.CommissionEntries.AsNoTracking(), paymentIds).ToListAsync(cancellationToken));

    public async Task<IReadOnlyList<OwnerRevenueDayRow>> SumByVietnamDayAsync(OwnerRevenueFilter filter, CancellationToken cancellationToken)
        => await OwnerRevenueQueryShapes.SumByVietnamDay(db.Payments.AsNoTracking(), filter).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OwnerRevenueDayHourRow>> SumByVietnamDayHourAsync(OwnerRevenueFilter filter,
        CancellationToken cancellationToken)
        => await OwnerRevenueQueryShapes.SumByVietnamDayHour(db.Payments.AsNoTracking(), filter).ToListAsync(cancellationToken);

    public async Task<PagedResult<OwnerRevenueTransactionRow>> ListTransactionsAsync(OwnerRevenueTransactionFilter filter, int page,
        int pageSize, CancellationToken cancellationToken)
    {
        var query = OwnerRevenueQueryShapes.FilterTransactions(db.Payments.AsNoTracking(), filter);
        var total = await query.CountAsync(cancellationToken);
        var items = await OwnerRevenueQueryShapes.ToTransactionRows(query.Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(cancellationToken);
        return new PagedResult<OwnerRevenueTransactionRow>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<OwnerRevenueTransactionRow>> ListTransactionsForExportAsync(OwnerRevenueTransactionFilter filter,
        int maxRows, CancellationToken cancellationToken)
        => await OwnerRevenueQueryShapes.ToTransactionRows(OwnerRevenueQueryShapes.FilterTransactions(db.Payments.AsNoTracking(), filter)
            .Take(maxRows)).ToListAsync(cancellationToken);
}
