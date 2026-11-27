using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;

namespace PaymentService.Domain.Services;

// ===== Đầu vào (đã lọc theo chủ bãi + mốc chốt; dữ liệu của chính kỳ đang tính lại KHÔNG nằm trong "đã quyết toán ở kỳ khác") =====

/// <summary>Giao dịch mới vào kỳ. RecognizedAtUtc = lúc booking kết thúc / thu tại cổng; BlockedReason ≠ null khi không xác minh được booking.</summary>
public sealed record SettlementPaymentInput(int PaymentId, string Code, string? BookingCode, int ParkingLotId, PaymentPurpose Purpose,
    PaymentMethod Method, decimal Amount, DateTime RecognizedAtUtc, string? BlockedReason = null);

/// <summary>Giao dịch đã nằm ở kỳ khác nhưng có hoàn tiền mới trong kỳ này.</summary>
public sealed record SettledPaymentInfo(int PaymentId, string Code, string? BookingCode, int ParkingLotId, PaymentPurpose Purpose,
    PaymentMethod Method);

/// <summary>Lần hoàn Completed trước mốc chốt, chưa nằm ở kỳ nào.</summary>
public sealed record SettlementRefundInput(int RefundId, int PaymentId, decimal Amount, DateTime ProcessedAtUtc);

/// <summary>Bút toán Accrual của giao dịch (US-079): tỷ lệ dùng cho mọi lần giảm hoa hồng sau đó.</summary>
public sealed record PaymentAccrual(int PaymentId, decimal Rate, decimal Amount);

/// <summary>Đã quyết toán ở kỳ khác cho một giao dịch: tổng tiền hoàn (dương) và tổng hoa hồng đã giảm (dương).</summary>
public sealed record PriorRefundState(int PaymentId, decimal RefundedSettled, decimal CommissionReversedSettled);

/// <summary>Khoản giữ đang mở (SupportService payout-holds).</summary>
public sealed record OpenHoldInput(string ComplaintCode, int ParkingLotId, decimal Amount);

/// <summary>Σ các dòng DisputeHold của một khiếu nại ở các kỳ khác (âm = đang giữ).</summary>
public sealed record HeldElsewhereInput(string ComplaintCode, int ParkingLotId, decimal NetAmount);

/// <summary>Voucher đền bù tính cho chủ bãi, phát hành trước mốc chốt.</summary>
public sealed record VoucherInput(string Code, decimal Amount, int IssuedByUserId, VoucherStatus Status, DateTime? UpdatedAtUtc);

/// <summary>Bút toán voucher đã có: ChargeAdjustmentId = bút toán trừ, ChargedAmount = giá trị đã trừ (dương); Reversed = đã có bút toán đảo.</summary>
public sealed record VoucherChargeState(string Code, int? ChargeAdjustmentId, decimal ChargedAmount, bool Reversed);

/// <summary>Bút toán Approved chưa gắn kỳ (hoặc đang gắn chính kỳ Draft này).</summary>
public sealed record AttachableAdjustment(int Id, decimal Amount);

public sealed record CarryOverInput(int SettlementId, decimal NetPayout);

public sealed record SettlementInput(
    DateTime CutoffUtc,
    DateTime NowUtc,
    IReadOnlyList<SettlementPaymentInput> Payments,
    IReadOnlyList<SettledPaymentInfo> SettledPayments,
    IReadOnlyList<SettlementRefundInput> Refunds,
    IReadOnlyList<PaymentAccrual> Accruals,
    IReadOnlyList<PriorRefundState> PriorRefunds,
    IReadOnlyCollection<PaymentPurpose> ExcludedPurposes,
    IReadOnlyList<OpenHoldInput>? OpenHolds,
    IReadOnlyList<HeldElsewhereInput> HeldElsewhere,
    IReadOnlyList<VoucherInput> Vouchers,
    IReadOnlyList<VoucherChargeState> VoucherCharges,
    IReadOnlyList<AttachableAdjustment> Adjustments,
    CarryOverInput? CarryOver);

// ===== Kết quả =====

public sealed record ComputedSettlementLine(SettlementLineType LineType, int? PaymentId, int? RefundId, int ParkingLotId, string? ReferenceCode,
    decimal Amount, decimal CommissionAmount, bool IsOwnerCollected, DateTime? OccurredAtUtc, string? BlockedReason);

/// <summary>Bút toán voucher hệ thống cần tạo: Amount = số tiền có dấu (âm = trừ chủ bãi).</summary>
public sealed record VoucherAdjustmentToCreate(string VoucherCode, decimal Amount, int IssuedByUserId, bool IsReversal, int? ReversesAdjustmentId);

public sealed record SettlementComputation(
    IReadOnlyList<ComputedSettlementLine> Lines,
    decimal Gross,
    decimal Refund,
    decimal OwnerCollected,
    decimal Commission,
    decimal EffectiveCommissionRate,
    decimal Held,
    decimal Adjustment,
    decimal CarryOver,
    int? CarryOverFromSettlementId,
    decimal NetPayout,
    IReadOnlyList<string> BlockedReasons,
    IReadOnlyList<VoucherAdjustmentToCreate> VoucherAdjustments,
    IReadOnlyList<int> AttachedAdjustmentIds)
{
    /// <summary>Không có giao dịch, hoàn tiền, khoản giữ, điều chỉnh hay số mang sang → không tạo kỳ cho chủ bãi.</summary>
    public bool HasContent => Lines.Count > 0 || VoucherAdjustments.Count > 0 || AttachedAdjustmentIds.Count > 0 || CarryOver != 0;
}

public static class SettlementBlockReasons
{
    /// <summary>Giao dịch chưa có bút toán Accrual (job US-079 chưa tính) – không đoán hoa hồng.</summary>
    public const string CommissionPending = "CommissionPending";
    /// <summary>Giao dịch đặt chỗ không có mã booking để xác minh trạng thái.</summary>
    public const string BookingCodeMissing = "BookingCodeMissing";
    /// <summary>BookingService không tìm thấy booking của giao dịch.</summary>
    public const string BookingNotFound = "BookingNotFound";
    /// <summary>Booking đã kết thúc nhưng không có lịch sử trạng thái để biết thời điểm kết thúc.</summary>
    public const string BookingUnverifiable = "BookingUnverifiable";
    /// <summary>Chưa lấy danh sách khoản giữ tranh chấp (job không có JWT Admin) – Admin bấm "Tính lại" để nạp.</summary>
    public const string HoldsNotLoaded = "HoldsNotLoaded";
}

/// <summary>
/// US-080: tính một kỳ quyết toán – hàm thuần. Công thức (xem <see cref="Settlement"/>):
/// NetPayout = Gross − Refund − OwnerCollected − Commission − Held + Adjustment + CarryOver.
/// Hoa hồng phần hoàn tính trên LŨY KẾ như US-079: tổng đã giảm = round(Σ hoàn trước mốc chốt × tỷ lệ Accrual); kỳ này giảm phần còn thiếu
/// so với các kỳ trước → hoàn hết thì hoa hồng của giao dịch về đúng 0, không lệch làm tròn và không phụ thuộc job Reversal chạy kịp hay chưa.
/// </summary>
public static class SettlementCalculator
{
    public static SettlementComputation Compute(SettlementInput input)
    {
        var excluded = input.ExcludedPurposes;
        var accruals = input.Accruals.GroupBy(a => a.PaymentId).ToDictionary(g => g.Key, g => g.First());
        var lines = new List<ComputedSettlementLine>();

        // 1. Giao dịch mới: số tiền gốc + hoa hồng Accrual.
        foreach (var p in input.Payments.OrderBy(p => p.RecognizedAtUtc).ThenBy(p => p.PaymentId))
        {
            var (lineCommission, pending) = PaymentCommission(p.PaymentId, p.Purpose, accruals, excluded);
            lines.Add(new ComputedSettlementLine(LineTypeOf(p.Purpose), p.PaymentId, null, p.ParkingLotId, Reference(p.BookingCode, p.Code),
                p.Amount, lineCommission, IsOwnerCollected(p.Method), p.RecognizedAtUtc,
                p.BlockedReason ?? (pending ? SettlementBlockReasons.CommissionPending : null)));
        }

        // 2. Hoàn tiền: của giao dịch vào kỳ này hoặc đã quyết toán ở kỳ trước.
        var paymentInfo = input.SettledPayments.ToDictionary(s => s.PaymentId);
        foreach (var p in input.Payments)
            paymentInfo[p.PaymentId] = new SettledPaymentInfo(p.PaymentId, p.Code, p.BookingCode, p.ParkingLotId, p.Purpose, p.Method);
        var prior = input.PriorRefunds.ToDictionary(r => r.PaymentId);

        foreach (var group in input.Refunds.Where(r => paymentInfo.ContainsKey(r.PaymentId)).GroupBy(r => r.PaymentId).OrderBy(g => g.Key))
        {
            var info = paymentInfo[group.Key];
            var refunds = group.OrderBy(r => r.ProcessedAtUtc).ThenBy(r => r.RefundId).ToList();
            var before = prior.GetValueOrDefault(group.Key) ?? new PriorRefundState(group.Key, 0, 0);
            var (reduction, pending) = RefundCommissionReduction(info.Purpose, accruals.GetValueOrDefault(group.Key), excluded,
                before.RefundedSettled + refunds.Sum(r => r.Amount), before.CommissionReversedSettled);

            for (var i = 0; i < refunds.Count; i++)
            {
                var r = refunds[i];
                var isLast = i == refunds.Count - 1;
                lines.Add(new ComputedSettlementLine(SettlementLineType.Refund, r.PaymentId, r.RefundId, info.ParkingLotId,
                    Reference(info.BookingCode, info.Code), -r.Amount, isLast ? -reduction : 0, IsOwnerCollected(info.Method), r.ProcessedAtUtc,
                    pending ? SettlementBlockReasons.CommissionPending : null));
            }
        }

        // 3. Khoản giữ tranh chấp: giữ mới các khiếu nại đang mở chưa giữ; giải phóng khiếu nại đã giữ ở kỳ trước nay đã đóng.
        var blocked = new List<string>();
        if (input.OpenHolds is null)
            blocked.Add(SettlementBlockReasons.HoldsNotLoaded);
        else
        {
            var heldElsewhere = input.HeldElsewhere.Where(h => h.NetAmount < 0).ToDictionary(h => h.ComplaintCode, StringComparer.Ordinal);
            var open = input.OpenHolds.Where(h => h.Amount > 0).GroupBy(h => h.ComplaintCode, StringComparer.Ordinal)
                .Select(g => g.First()).ToList();
            foreach (var hold in open.Where(h => !heldElsewhere.ContainsKey(h.ComplaintCode)).OrderBy(h => h.ComplaintCode, StringComparer.Ordinal))
                lines.Add(new ComputedSettlementLine(SettlementLineType.DisputeHold, null, null, hold.ParkingLotId, hold.ComplaintCode,
                    -hold.Amount, 0, false, input.NowUtc, null));
            var openCodes = open.Select(h => h.ComplaintCode).ToHashSet(StringComparer.Ordinal);
            foreach (var release in heldElsewhere.Values.Where(h => !openCodes.Contains(h.ComplaintCode)).OrderBy(h => h.ComplaintCode, StringComparer.Ordinal))
                lines.Add(new ComputedSettlementLine(SettlementLineType.DisputeHold, null, null, release.ParkingLotId, release.ComplaintCode,
                    -release.NetAmount, 0, false, input.NowUtc, null));
        }

        // 4. Voucher đền bù tính cho chủ bãi: trừ một lần; thu hồi sau khi đã trừ → bút toán đảo.
        var charges = input.VoucherCharges.ToDictionary(c => c.Code, StringComparer.Ordinal);
        var voucherAdjustments = new List<VoucherAdjustmentToCreate>();
        foreach (var v in input.Vouchers.OrderBy(v => v.Code, StringComparer.Ordinal))
        {
            var charge = charges.GetValueOrDefault(v.Code);
            if (v.Status != VoucherStatus.Revoked)
            {
                if (charge is null && v.Amount > 0)
                    voucherAdjustments.Add(new VoucherAdjustmentToCreate(v.Code, -v.Amount, v.IssuedByUserId, false, null));
            }
            else if (charge is { Reversed: false } && v.UpdatedAtUtc < input.CutoffUtc)
                voucherAdjustments.Add(new VoucherAdjustmentToCreate(v.Code, charge.ChargedAmount, v.IssuedByUserId, true, charge.ChargeAdjustmentId));
        }

        // 5. Tổng.
        var gross = lines.Where(IsPaymentLine).Sum(l => l.Amount);
        var refund = -lines.Where(l => l.LineType == SettlementLineType.Refund).Sum(l => l.Amount);
        var ownerCollected = lines.Where(l => l.IsOwnerCollected).Sum(l => l.Amount);
        var commission = lines.Sum(l => l.CommissionAmount);
        var held = -lines.Where(l => l.LineType == SettlementLineType.DisputeHold).Sum(l => l.Amount);
        var adjustment = input.Adjustments.Sum(a => a.Amount) + voucherAdjustments.Sum(a => a.Amount);
        var carryOver = input.CarryOver is { NetPayout: < 0 } c ? c.NetPayout : 0m;
        var net = gross - refund - ownerCollected - commission - held + adjustment + carryOver;

        blocked.InsertRange(0, lines.Select(l => l.BlockedReason).OfType<string>().Distinct());

        return new SettlementComputation(lines, gross, refund, ownerCollected, commission, EffectiveRate(commission, gross), held, adjustment,
            carryOver, carryOver != 0 ? input.CarryOver!.SettlementId : null, net, blocked.Distinct().ToList(), voucherAdjustments,
            input.Adjustments.Select(a => a.Id).ToList());
    }

    /// <summary>WalkIn / Overstay (thu tại cổng) → WalkIn; Booking / Extension / MonthlyPass → Booking.</summary>
    public static SettlementLineType LineTypeOf(PaymentPurpose purpose)
        => purpose is PaymentPurpose.WalkIn or PaymentPurpose.Overstay ? SettlementLineType.WalkIn : SettlementLineType.Booking;

    /// <summary>Cash và VietQR (QR vào thẳng tài khoản chủ bãi) – nền tảng không giữ tiền.</summary>
    public static bool IsOwnerCollected(PaymentMethod method) => method is PaymentMethod.Cash or PaymentMethod.VietQr;

    /// <summary>Tỷ lệ hiệu dụng Commission / Gross, 4 chữ số (cột numeric(5,4)); Gross = 0 → 0.</summary>
    public static decimal EffectiveRate(decimal commission, decimal gross)
        => gross <= 0 ? 0 : Math.Clamp(Math.Round(commission / gross, 4, MidpointRounding.AwayFromZero), -9.9999m, 9.9999m);

    private static bool IsPaymentLine(ComputedSettlementLine l) => l.LineType is SettlementLineType.Booking or SettlementLineType.WalkIn;

    private static string Reference(string? bookingCode, string paymentCode)
    {
        var value = string.IsNullOrWhiteSpace(bookingCode) ? paymentCode : bookingCode.Trim();
        return value.Length <= 30 ? value : value[..30];
    }

    private static (decimal Commission, bool Pending) PaymentCommission(int paymentId, PaymentPurpose purpose,
        IReadOnlyDictionary<int, PaymentAccrual> accruals, IReadOnlyCollection<PaymentPurpose> excluded)
    {
        if (excluded.Contains(purpose)) return (0, false);
        return accruals.TryGetValue(paymentId, out var accrual) ? (accrual.Amount, false) : (0, true);
    }

    /// <summary>Phần hoa hồng được giảm trong kỳ (số dương) cho các lần hoàn mới của một giao dịch.</summary>
    private static (decimal Reduction, bool Pending) RefundCommissionReduction(PaymentPurpose purpose, PaymentAccrual? accrual,
        IReadOnlyCollection<PaymentPurpose> excluded, decimal refundedCumulative, decimal reversedSoFar)
    {
        if (excluded.Contains(purpose)) return (0, false);
        if (accrual is null) return (0, true);
        var target = Math.Min(CommissionEntry.RoundVnd(refundedCumulative * accrual.Rate), accrual.Amount);
        return (Math.Max(0, target - reversedSoFar), false);
    }
}
