using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features.Settlements;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Services;

namespace PaymentService.Infrastructure.Persistence.Queries;

/// <summary>
/// US-080: hình dạng truy vấn sinh kỳ quyết toán (lọc / GROUP BY / SUM ở database). Viết trên IQueryable để test chạy đúng logic này
/// trong bộ nhớ và kiểm tra SQL bằng ToQueryString. <c>exclude</c> = kỳ Draft đang tính lại: dòng / bút toán của nó coi như chưa có.
/// </summary>
public static class SettlementQueryShapes
{
    private static readonly PaymentStatus[] Collected = [PaymentStatus.Succeeded, PaymentStatus.PartiallyRefunded, PaymentStatus.Refunded];
    private static readonly SettlementStatus[] Open = [SettlementStatus.Draft, SettlementStatus.PendingReview];
    private static readonly SettlementStatus[] Final = [SettlementStatus.Approved, SettlementStatus.Paid];

    /// <summary>Dòng giao dịch (Booking / WalkIn) của các kỳ khác.</summary>
    public static IQueryable<SettlementLine> SaleLines(IQueryable<SettlementLine> lines, int? exclude)
        => lines.Where(l => l.PaymentId != null
                            && (l.LineType == SettlementLineType.Booking || l.LineType == SettlementLineType.WalkIn)
                            && (exclude == null || l.SettlementId != exclude));

    public static IQueryable<SettlementLine> RefundLines(IQueryable<SettlementLine> lines, int? exclude)
        => lines.Where(l => l.RefundId != null && l.LineType == SettlementLineType.Refund && (exclude == null || l.SettlementId != exclude));

    /// <summary>Giao dịch đã thu trước mốc chốt, chưa nằm ở kỳ nào (index Payments(OwnerProfileId, Status, PaidAtUtc)).</summary>
    public static IQueryable<UnsettledPaymentRow> UnsettledPayments(IQueryable<Payment> payments, IQueryable<SettlementLine> lines,
        int ownerProfileId, DateTime cutoffUtc, int? exclude)
    {
        var sale = SaleLines(lines, exclude);
        return payments
            .Where(p => p.OwnerProfileId == ownerProfileId && Collected.Contains(p.Status) && p.PaidAtUtc != null && p.PaidAtUtc < cutoffUtc
                        && !sale.Any(l => l.PaymentId == p.Id))
            .OrderBy(p => p.PaidAtUtc).ThenBy(p => p.Id)
            .Select(p => new UnsettledPaymentRow(p.Id, p.Code, p.BookingCode, p.ParkingLotId, p.Purpose, p.Method, p.Amount, p.PaidAtUtc!.Value));
    }

    /// <summary>Lần hoàn Completed trước mốc chốt chưa nằm ở kỳ nào; PaymentSettled = giao dịch gốc đã ở kỳ khác.</summary>
    public static IQueryable<UnsettledRefundRow> UnsettledRefunds(IQueryable<Refund> refunds, IQueryable<Payment> payments,
        IQueryable<SettlementLine> lines, int ownerProfileId, DateTime cutoffUtc, int? exclude)
    {
        var sale = SaleLines(lines, exclude);
        var refunded = RefundLines(lines, exclude);
        return from r in refunds
               join p in payments on r.PaymentId equals p.Id
               where p.OwnerProfileId == ownerProfileId && r.Status == RefundStatus.Completed
                     && r.ProcessedAtUtc != null && r.ProcessedAtUtc < cutoffUtc
                     && !refunded.Any(l => l.RefundId == r.Id)
               orderby r.ProcessedAtUtc, r.Id
               select new UnsettledRefundRow(r.Id, r.PaymentId, r.Amount, r.ProcessedAtUtc!.Value, sale.Any(l => l.PaymentId == r.PaymentId));
    }

    public static IQueryable<SettledPaymentInfo> PaymentInfo(IQueryable<Payment> payments, IReadOnlyCollection<int> paymentIds)
        => payments.Where(p => paymentIds.Contains(p.Id))
            .Select(p => new SettledPaymentInfo(p.Id, p.Code, p.BookingCode, p.ParkingLotId, p.Purpose, p.Method));

    public static IQueryable<PaymentAccrual> Accruals(IQueryable<CommissionEntry> entries, IReadOnlyCollection<int> paymentIds)
        => entries.Where(e => e.EntryType == CommissionEntryType.Accrual && paymentIds.Contains(e.PaymentId))
            .Select(e => new PaymentAccrual(e.PaymentId, e.Rate, e.Amount));

    /// <summary>Đã quyết toán ở kỳ khác: tổng tiền hoàn và tổng hoa hồng đã giảm (đều dương) theo giao dịch.</summary>
    public static IQueryable<PriorRefundState> PriorRefunds(IQueryable<SettlementLine> lines, IReadOnlyCollection<int> paymentIds, int? exclude)
        => RefundLines(lines, exclude)
            .Where(l => paymentIds.Contains(l.PaymentId!.Value))
            .GroupBy(l => l.PaymentId!.Value)
            .Select(g => new PriorRefundState(g.Key, -g.Sum(l => l.Amount), -g.Sum(l => l.CommissionAmount)));

    /// <summary>Σ dòng DisputeHold theo khiếu nại ở các kỳ khác của chủ bãi (âm = đang giữ).</summary>
    public static IQueryable<HeldElsewhereInput> HeldElsewhere(IQueryable<SettlementLine> lines, IQueryable<Settlement> settlements,
        int ownerProfileId, int? exclude)
        => from l in lines
           join s in settlements on l.SettlementId equals s.Id
           where s.OwnerProfileId == ownerProfileId && l.LineType == SettlementLineType.DisputeHold && l.ReferenceCode != null
                 && (exclude == null || s.Id != exclude)
           group l by l.ReferenceCode into g
           select new HeldElsewhereInput(g.Key!, g.Max(l => l.ParkingLotId), g.Sum(l => l.Amount));

    public static IQueryable<VoucherRow> Vouchers(IQueryable<CompensationVoucher> vouchers, int ownerProfileId, DateTime cutoffUtc)
        => vouchers.Where(v => v.ChargedToOwnerProfileId == ownerProfileId && v.CreatedAtUtc < cutoffUtc)
            .OrderBy(v => v.Code)
            .Select(v => new VoucherRow(v.Code, v.Amount, v.IssuedByUserId, v.Status, v.UpdatedAtUtc));

    /// <summary>Bút toán voucher của chủ bãi (Id, SourceReference, Amount) – ghép thành <see cref="VoucherChargeState"/> ở bộ nhớ.</summary>
    public static IQueryable<FinancialAdjustment> VoucherAdjustments(IQueryable<FinancialAdjustment> adjustments, int ownerProfileId)
        => adjustments.Where(a => a.OwnerProfileId == ownerProfileId && a.SourceType == FinancialAdjustmentSource.CompensationVoucher);

    public static IReadOnlyList<VoucherChargeState> ToVoucherCharges(IEnumerable<(int Id, string? SourceReference, decimal Amount)> rows)
    {
        var list = rows.Where(r => r.SourceReference is not null).ToList();
        var reversed = list.Where(r => r.SourceReference!.EndsWith(FinancialAdjustment.RevokedSuffix, StringComparison.Ordinal))
            .Select(r => r.SourceReference![..^FinancialAdjustment.RevokedSuffix.Length]).ToHashSet(StringComparer.Ordinal);
        return [.. list.Where(r => !r.SourceReference!.EndsWith(FinancialAdjustment.RevokedSuffix, StringComparison.Ordinal))
            .Select(r => new VoucherChargeState(r.SourceReference!, r.Id, -r.Amount, reversed.Contains(r.SourceReference!)))];
    }

    /// <summary>Bút toán đã duyệt (trước lúc sinh) chưa gắn kỳ, hoặc đang gắn chính kỳ Draft đang tính lại.</summary>
    public static IQueryable<AttachableAdjustment> AttachableAdjustments(IQueryable<FinancialAdjustment> adjustments, int ownerProfileId,
        DateTime nowUtc, int? exclude)
        => adjustments.Where(a => a.OwnerProfileId == ownerProfileId && a.Status == FinancialAdjustmentStatus.Approved
                                  && a.ApprovedAtUtc != null && a.ApprovedAtUtc <= nowUtc
                                  && (a.SettlementId == null || (exclude != null && a.SettlementId == exclude)))
            .OrderBy(a => a.Id)
            .Select(a => new AttachableAdjustment(a.Id, a.Amount));

    /// <summary>Kỳ đã duyệt / đã chi có NetPayout âm, chưa được kỳ nào khác mang sang (mới nhất).</summary>
    public static IQueryable<CarryOverInput> CarryOver(IQueryable<Settlement> settlements, int ownerProfileId, int? exclude)
        => settlements
            .Where(s => s.OwnerProfileId == ownerProfileId && Final.Contains(s.Status) && s.NetPayout < 0 && (exclude == null || s.Id != exclude)
                        && !settlements.Any(o => o.CarryOverFromSettlementId == s.Id && (exclude == null || o.Id != exclude)))
            .OrderByDescending(s => s.PeriodStart)
            .Select(s => new CarryOverInput(s.Id, s.NetPayout));

    public static IQueryable<Settlement> OwnerSettlements(IQueryable<Settlement> settlements, int ownerProfileId)
        => settlements.Where(s => s.OwnerProfileId == ownerProfileId);

    public static OwnerSettlementState ToOwnerState(IEnumerable<(int Id, DateOnly PeriodStart, SettlementStatus Status)> rows, DateOnly periodStart)
    {
        var list = rows.ToList();
        var existing = list.FirstOrDefault(r => r.PeriodStart == periodStart);
        return new OwnerSettlementState(existing.Id == 0 ? null : existing.Id, existing.Id == 0 ? null : existing.Status,
            list.Any(r => r.PeriodStart < periodStart && Open.Contains(r.Status)), list.Any(r => r.PeriodStart > periodStart));
    }

    // ===== Chủ bãi cần sinh kỳ =====

    public static IQueryable<int> OwnersWithUnsettledPayments(IQueryable<Payment> payments, IQueryable<SettlementLine> lines, DateTime cutoffUtc)
    {
        var sale = SaleLines(lines, null);
        return payments.Where(p => Collected.Contains(p.Status) && p.PaidAtUtc != null && p.PaidAtUtc < cutoffUtc
                                   && !sale.Any(l => l.PaymentId == p.Id))
            .Select(p => p.OwnerProfileId).Distinct();
    }

    public static IQueryable<int> OwnersWithUnsettledRefunds(IQueryable<Refund> refunds, IQueryable<Payment> payments,
        IQueryable<SettlementLine> lines, DateTime cutoffUtc)
    {
        var refunded = RefundLines(lines, null);
        return (from r in refunds
                join p in payments on r.PaymentId equals p.Id
                where r.Status == RefundStatus.Completed && r.ProcessedAtUtc != null && r.ProcessedAtUtc < cutoffUtc
                      && !refunded.Any(l => l.RefundId == r.Id)
                select p.OwnerProfileId).Distinct();
    }

    public static IQueryable<int> OwnersWithApprovedAdjustments(IQueryable<FinancialAdjustment> adjustments)
        => adjustments.Where(a => a.Status == FinancialAdjustmentStatus.Approved && a.SettlementId == null)
            .Select(a => a.OwnerProfileId).Distinct();

    public static IQueryable<int> OwnersWithVouchersToCharge(IQueryable<CompensationVoucher> vouchers, IQueryable<FinancialAdjustment> adjustments,
        DateTime cutoffUtc)
    {
        var voucherAdjustments = adjustments.Where(a => a.SourceType == FinancialAdjustmentSource.CompensationVoucher);
        return vouchers.Where(v => v.ChargedToOwnerProfileId != null && v.CreatedAtUtc < cutoffUtc
                                   && (v.Status != VoucherStatus.Revoked
                                       ? !voucherAdjustments.Any(a => a.SourceReference == v.Code)
                                       : voucherAdjustments.Any(a => a.SourceReference == v.Code)
                                         && !voucherAdjustments.Any(a => a.SourceReference == v.Code + FinancialAdjustment.RevokedSuffix)))
            .Select(v => v.ChargedToOwnerProfileId!.Value).Distinct();
    }

    public static IQueryable<int> OwnersWithCarryOver(IQueryable<Settlement> settlements)
        => settlements.Where(s => Final.Contains(s.Status) && s.NetPayout < 0 && !settlements.Any(o => o.CarryOverFromSettlementId == s.Id))
            .Select(s => s.OwnerProfileId).Distinct();

    public static IQueryable<int> OwnersWithOpenHolds(IQueryable<SettlementLine> lines, IQueryable<Settlement> settlements)
        => (from l in lines
            join s in settlements on l.SettlementId equals s.Id
            where l.LineType == SettlementLineType.DisputeHold && l.ReferenceCode != null
            group l by new { s.OwnerProfileId, l.ReferenceCode } into g
            where g.Sum(l => l.Amount) < 0
            select g.Key.OwnerProfileId).Distinct();
}
