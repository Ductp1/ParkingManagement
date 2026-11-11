using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features.Commission;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Queries;

/// <summary>
/// US-079: hình dạng truy vấn sổ hoa hồng – dùng chung cho EF (dịch sang SQL) và test chạy trong bộ nhớ, như OwnerRevenueQueryShapes.
/// Payment không có navigation sang CommissionEntry nên mọi liên kết là subquery tương quan theo PaymentId.
/// </summary>
public static class CommissionQueryShapes
{
    /// <summary>Trạng thái được tính doanh thu / hoa hồng (khớp OwnerRevenueRules.RevenueStatuses).</summary>
    public static IQueryable<Payment> WhereCollected(this IQueryable<Payment> payments)
        => payments.Where(p => p.PaidAtUtc != null
            && (p.Status == PaymentStatus.Succeeded || p.Status == PaymentStatus.PartiallyRefunded || p.Status == PaymentStatus.Refunded));

    /// <summary>Bỏ các mục đích bị loại. Danh sách rỗng → không thêm điều kiện (SQL gọn).</summary>
    public static IQueryable<Payment> WhereNotExcluded(this IQueryable<Payment> payments, IReadOnlyCollection<PaymentPurpose> excludedPurposes)
    {
        if (excludedPurposes.Count == 0) return payments;
        var excluded = excludedPurposes.ToList();      // List.Contains – tránh overload Span của array.Contains trong expression tree
        return payments.Where(p => !excluded.Contains(p.Purpose));
    }

    /// <summary>Giao dịch đã thu tiền, chưa có Accrual – cũ nhất trước để backfill theo thứ tự thời gian.</summary>
    public static IQueryable<CommissionAccrualCandidate> AccrualCandidates(IQueryable<Payment> payments, IQueryable<CommissionEntry> entries,
        IReadOnlyCollection<PaymentPurpose> excludedPurposes, int take)
        => payments.WhereCollected().WhereNotExcluded(excludedPurposes)
            .Where(p => !entries.Any(e => e.PaymentId == p.Id && e.EntryType == CommissionEntryType.Accrual))
            .OrderBy(p => p.PaidAtUtc).ThenBy(p => p.Id)
            .Take(take)
            .Select(p => new CommissionAccrualCandidate(p.Id, p.ParkingLotId, p.OwnerProfileId, p.Amount, p.PaidAtUtc!.Value));

    /// <summary>Giao dịch có Accrual mà RefundedAmount vượt mức đã bù bởi các Reversal.</summary>
    public static IQueryable<CommissionReversalCandidate> ReversalCandidates(IQueryable<Payment> payments, IQueryable<CommissionEntry> entries,
        int take)
        => entries.Where(a => a.EntryType == CommissionEntryType.Accrual)
            .Join(payments, a => a.PaymentId, p => p.Id, (a, p) => new { a, p })
            .Where(x => x.p.RefundedAmount > 0
                && !entries.Any(r => r.PaymentId == x.p.Id && r.EntryType == CommissionEntryType.Reversal
                                     && r.RefundedAmountCovered >= x.p.RefundedAmount))
            .OrderBy(x => x.a.PaymentId)
            .Take(take)
            .Select(x => new CommissionReversalCandidate(x.a.PaymentId, x.a.ParkingLotId, x.a.OwnerProfileId, x.a.PaidAtUtc, x.p.RefundedAmount,
                x.a.Rate, x.a.RateSource, x.a.ContractNumber, x.a.ContractVersion,
                entries.Where(r => r.PaymentId == x.a.PaymentId && r.EntryType == CommissionEntryType.Reversal)
                    .Max(r => (decimal?)r.RefundedAmountCovered) ?? 0,
                -(entries.Where(r => r.PaymentId == x.a.PaymentId && r.EntryType == CommissionEntryType.Reversal)
                    .Sum(r => (decimal?)r.Amount) ?? 0)));

    /// <summary>
    /// Giao dịch còn chờ tính hoa hồng: chưa có Accrual (và không bị loại), hoặc đã hoàn thêm mà chưa có Reversal bù tới mức đó.
    /// </summary>
    public static IQueryable<Payment> WherePendingCommission(this IQueryable<Payment> payments, IQueryable<CommissionEntry> entries,
        IReadOnlyCollection<PaymentPurpose> excludedPurposes)
    {
        var excluded = excludedPurposes.ToList();
        var anyExcluded = excluded.Count > 0;
        return payments.Where(p =>
            (!entries.Any(e => e.PaymentId == p.Id && e.EntryType == CommissionEntryType.Accrual)
             && !(anyExcluded && excluded.Contains(p.Purpose)))
            || (p.RefundedAmount > 0
                && entries.Any(e => e.PaymentId == p.Id && e.EntryType == CommissionEntryType.Accrual)
                && !entries.Any(e => e.PaymentId == p.Id && e.EntryType == CommissionEntryType.Reversal
                                     && e.RefundedAmountCovered >= p.RefundedAmount)));
    }
}
