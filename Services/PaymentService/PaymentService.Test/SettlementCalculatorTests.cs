using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Services;

namespace PaymentService.Test;

// US-080: công thức NetPayout = Gross − Refund − OwnerCollected − Commission − Held + Adjustment + CarryOver (hàm thuần).
public class SettlementCalculatorTests
{
    private static readonly DateTime Cutoff = new(2026, 11, 8, 17, 0, 0, DateTimeKind.Utc);    // hết Chủ nhật 08/11 ICT
    private static readonly DateTime Now = new(2026, 11, 10, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime InPeriod = new(2026, 11, 4, 2, 0, 0, DateTimeKind.Utc);

    private static SettlementPaymentInput Pay(int id, decimal amount, PaymentMethod method = PaymentMethod.Vnpay,
        PaymentPurpose purpose = PaymentPurpose.Booking, string? blocked = null, int lot = 11)
        => new(id, $"PM-{id}", $"BK-{id}", lot, purpose, method, amount, InPeriod, blocked);

    private static SettlementInput Input(
        IReadOnlyList<SettlementPaymentInput>? payments = null,
        IReadOnlyList<PaymentAccrual>? accruals = null,
        IReadOnlyList<SettlementRefundInput>? refunds = null,
        IReadOnlyList<SettledPaymentInfo>? settled = null,
        IReadOnlyList<PriorRefundState>? prior = null,
        IReadOnlyCollection<PaymentPurpose>? excluded = null,
        IReadOnlyList<OpenHoldInput>? holds = null,
        bool holdsLoaded = true,
        IReadOnlyList<HeldElsewhereInput>? heldElsewhere = null,
        IReadOnlyList<VoucherInput>? vouchers = null,
        IReadOnlyList<VoucherChargeState>? charges = null,
        IReadOnlyList<AttachableAdjustment>? adjustments = null,
        CarryOverInput? carryOver = null)
        => new(Cutoff, Now, payments ?? [], settled ?? [], refunds ?? [], accruals ?? [], prior ?? [], excluded ?? [],
            holdsLoaded ? holds ?? [] : null, heldElsewhere ?? [], vouchers ?? [], charges ?? [], adjustments ?? [], carryOver);

    [Fact]
    public void OnlinePayment_NetIsGrossMinusCommission()
    {
        var result = SettlementCalculator.Compute(Input([Pay(1, 100_000)], [new PaymentAccrual(1, 0.10m, 10_000)]));

        Assert.Equal(100_000, result.Gross);
        Assert.Equal(10_000, result.Commission);
        Assert.Equal(0, result.OwnerCollected);
        Assert.Equal(90_000, result.NetPayout);
        Assert.Equal(0.1m, result.EffectiveCommissionRate);
        Assert.Empty(result.BlockedReasons);
        var line = Assert.Single(result.Lines);
        Assert.Equal(SettlementLineType.Booking, line.LineType);
        Assert.Equal("BK-1", line.ReferenceCode);
        Assert.False(line.IsOwnerCollected);
    }

    [Theory]
    [InlineData(PaymentMethod.Cash)]
    [InlineData(PaymentMethod.VietQr)]
    public void OwnerCollectedPayment_IsNotPaidOut_ButCommissionIsStillDeducted(PaymentMethod method)
    {
        var result = SettlementCalculator.Compute(Input([Pay(1, 100_000, method, PaymentPurpose.WalkIn)],
            [new PaymentAccrual(1, 0.10m, 10_000)]));

        Assert.Equal(100_000, result.OwnerCollected);
        Assert.Equal(-10_000, result.NetPayout);           // chủ bãi nợ nền tảng phần hoa hồng
        Assert.Equal(SettlementLineType.WalkIn, Assert.Single(result.Lines).LineType);
        Assert.True(result.Lines[0].IsOwnerCollected);
    }

    [Fact]
    public void MixedMethods_SumCorrectly()
    {
        var result = SettlementCalculator.Compute(Input(
            [Pay(1, 200_000), Pay(2, 50_000, PaymentMethod.VietQr), Pay(3, 30_000, PaymentMethod.Momo, PaymentPurpose.Extension)],
            [new PaymentAccrual(1, 0.10m, 20_000), new PaymentAccrual(2, 0.10m, 5_000), new PaymentAccrual(3, 0.075m, 2_250)]));

        Assert.Equal(280_000, result.Gross);
        Assert.Equal(50_000, result.OwnerCollected);
        Assert.Equal(27_250, result.Commission);
        Assert.Equal(280_000 - 50_000 - 27_250, result.NetPayout);
        Assert.Equal(0.0973m, result.EffectiveCommissionRate);       // 27 250 / 280 000 = 0.09732…
    }

    [Theory]
    [InlineData(PaymentPurpose.WalkIn, SettlementLineType.WalkIn)]
    [InlineData(PaymentPurpose.Overstay, SettlementLineType.WalkIn)]
    [InlineData(PaymentPurpose.Booking, SettlementLineType.Booking)]
    [InlineData(PaymentPurpose.Extension, SettlementLineType.Booking)]
    [InlineData(PaymentPurpose.MonthlyPass, SettlementLineType.Booking)]
    public void Purpose_MapsToLineType(PaymentPurpose purpose, SettlementLineType expected)
        => Assert.Equal(expected, SettlementCalculator.LineTypeOf(purpose));

    [Fact]
    public void MissingAccrual_BlocksWithCommissionPending_NoGuessedCommission()
    {
        var result = SettlementCalculator.Compute(Input([Pay(1, 100_000), Pay(2, 50_000)], [new PaymentAccrual(1, 0.10m, 10_000)]));

        Assert.Equal([SettlementBlockReasons.CommissionPending], result.BlockedReasons);
        Assert.Equal(10_000, result.Commission);
        Assert.Equal(SettlementBlockReasons.CommissionPending, result.Lines.Single(l => l.PaymentId == 2).BlockedReason);
        Assert.Null(result.Lines.Single(l => l.PaymentId == 1).BlockedReason);
    }

    [Fact]
    public void ExcludedPurpose_HasZeroCommission_AndDoesNotBlock()
    {
        var result = SettlementCalculator.Compute(Input([Pay(1, 300_000, purpose: PaymentPurpose.MonthlyPass)],
            excluded: [PaymentPurpose.MonthlyPass]));

        Assert.Equal(0, result.Commission);
        Assert.Equal(300_000, result.NetPayout);
        Assert.Empty(result.BlockedReasons);
    }

    [Fact]
    public void BookingReason_TakesPrecedenceOnLine()
    {
        var result = SettlementCalculator.Compute(Input([Pay(1, 100_000, blocked: SettlementBlockReasons.BookingNotFound)]));
        Assert.Equal(SettlementBlockReasons.BookingNotFound, Assert.Single(result.Lines).BlockedReason);
        Assert.Equal([SettlementBlockReasons.BookingNotFound], result.BlockedReasons);
    }

    [Fact]
    public void RefundInSamePeriod_DeductsRefundAndReducesCommission()
    {
        var result = SettlementCalculator.Compute(Input([Pay(1, 100_000)], [new PaymentAccrual(1, 0.075m, 7_500)],
            [new SettlementRefundInput(9, 1, 30_000, InPeriod.AddHours(1))]));

        var refund = result.Lines.Single(l => l.LineType == SettlementLineType.Refund);
        Assert.Equal(-30_000, refund.Amount);
        Assert.Equal(-2_250, refund.CommissionAmount);             // round(30 000 × 7,5%)
        Assert.Equal(9, refund.RefundId);
        Assert.Equal(1, refund.PaymentId);
        Assert.Equal(30_000, result.Refund);
        Assert.Equal(5_250, result.Commission);
        Assert.Equal(100_000 - 30_000 - 5_250, result.NetPayout);
    }

    [Fact]
    public void LateRefundOfEarlierSettledPayment_GoesToThisPeriod()
    {
        var result = SettlementCalculator.Compute(Input(
            accruals: [new PaymentAccrual(1, 0.10m, 10_000)],
            refunds: [new SettlementRefundInput(9, 1, 40_000, InPeriod)],
            settled: [new SettledPaymentInfo(1, "PM-1", "BK-1", 11, PaymentPurpose.Booking, PaymentMethod.Vnpay)]));

        Assert.Equal(0, result.Gross);
        Assert.Equal(40_000, result.Refund);
        Assert.Equal(-4_000, result.Commission);
        Assert.Equal(-36_000, result.NetPayout);
        Assert.Equal("BK-1", Assert.Single(result.Lines).ReferenceCode);
    }

    [Fact]
    public void CumulativeRefund_ReducesCommissionToExactlyZero_WithoutRoundingDrift()
    {
        // 12 345 × 7,5% = 925,875 → Accrual 926. Kỳ trước đã hoàn 5 000 (giảm 375); kỳ này hoàn nốt 7 345 → giảm 551 → tổng 926.
        var result = SettlementCalculator.Compute(Input(
            accruals: [new PaymentAccrual(1, 0.075m, 926)],
            refunds: [new SettlementRefundInput(9, 1, 7_345, InPeriod)],
            settled: [new SettledPaymentInfo(1, "PM-1", "BK-1", 11, PaymentPurpose.Booking, PaymentMethod.Vnpay)],
            prior: [new PriorRefundState(1, 5_000, 375)]));

        Assert.Equal(-551, Assert.Single(result.Lines).CommissionAmount);
        Assert.Equal(926, 375 + 551);
    }

    [Fact]
    public void SeveralRefundsOfOnePayment_CommissionReductionOnLastLineOnly()
    {
        var result = SettlementCalculator.Compute(Input([Pay(1, 100_000)], [new PaymentAccrual(1, 0.10m, 10_000)],
            [new SettlementRefundInput(8, 1, 10_000, InPeriod.AddHours(2)), new SettlementRefundInput(7, 1, 15_000, InPeriod.AddHours(1))]));

        var refunds = result.Lines.Where(l => l.LineType == SettlementLineType.Refund).ToList();
        Assert.Equal([7, 8], refunds.Select(r => r.RefundId!.Value));          // theo thời điểm hoàn
        Assert.Equal([0m, -2_500m], refunds.Select(r => r.CommissionAmount));
    }

    [Fact]
    public void CashRefundLater_ReturnsOnlyCommissionToOwner()
    {
        var result = SettlementCalculator.Compute(Input(
            accruals: [new PaymentAccrual(1, 0.10m, 10_000)],
            refunds: [new SettlementRefundInput(9, 1, 100_000, InPeriod)],
            settled: [new SettledPaymentInfo(1, "PM-1", null, 11, PaymentPurpose.WalkIn, PaymentMethod.Cash)]));

        Assert.Equal(-100_000, result.OwnerCollected);
        Assert.Equal(10_000, result.NetPayout);                     // chủ bãi tự trả khách, nền tảng trả lại hoa hồng
        Assert.Equal("PM-1", result.Lines[0].ReferenceCode);
    }

    [Fact]
    public void RefundWithoutAccrual_BlocksCommissionPending()
    {
        var result = SettlementCalculator.Compute(Input(
            refunds: [new SettlementRefundInput(9, 1, 10_000, InPeriod)],
            settled: [new SettledPaymentInfo(1, "PM-1", "BK-1", 11, PaymentPurpose.Booking, PaymentMethod.Vnpay)]));
        Assert.Contains(SettlementBlockReasons.CommissionPending, result.BlockedReasons);
    }

    [Fact]
    public void RefundOfPaymentOutsideScope_IsIgnored()
    {
        var result = SettlementCalculator.Compute(Input(refunds: [new SettlementRefundInput(9, 42, 10_000, InPeriod)]));
        Assert.Empty(result.Lines);
        Assert.False(result.HasContent);
    }

    [Fact]
    public void Holds_NewHoldReleaseAndAlreadyHeld()
    {
        var result = SettlementCalculator.Compute(Input([Pay(1, 200_000)], [new PaymentAccrual(1, 0.10m, 20_000)],
            holds: [new OpenHoldInput("CP-NEW", 11, 50_000), new OpenHoldInput("CP-STILL", 12, 20_000)],
            heldElsewhere: [new HeldElsewhereInput("CP-OLD", 12, -30_000), new HeldElsewhereInput("CP-STILL", 12, -20_000),
                new HeldElsewhereInput("CP-DONE", 12, 0)]));

        var holds = result.Lines.Where(l => l.LineType == SettlementLineType.DisputeHold).ToList();
        Assert.Equal(2, holds.Count);
        Assert.Equal(-50_000, holds.Single(h => h.ReferenceCode == "CP-NEW").Amount);       // giữ mới
        Assert.Equal(30_000, holds.Single(h => h.ReferenceCode == "CP-OLD").Amount);        // khiếu nại đã đóng → giải phóng
        Assert.Equal(20_000, result.Held);
        Assert.Equal(200_000 - 20_000 - 20_000, result.NetPayout);
        Assert.Empty(result.BlockedReasons);
    }

    [Fact]
    public void HoldsNotLoaded_BlocksAndAddsNoHoldLines()
    {
        var result = SettlementCalculator.Compute(Input([Pay(1, 100_000)], [new PaymentAccrual(1, 0.10m, 10_000)], holdsLoaded: false,
            heldElsewhere: [new HeldElsewhereInput("CP-OLD", 12, -30_000)]));

        Assert.Equal([SettlementBlockReasons.HoldsNotLoaded], result.BlockedReasons);
        Assert.DoesNotContain(result.Lines, l => l.LineType == SettlementLineType.DisputeHold);
    }

    [Fact]
    public void Vouchers_ChargeOnce_ReverseWhenRevokedBeforeCutoff()
    {
        var result = SettlementCalculator.Compute(Input(
            vouchers:
            [
                new VoucherInput("COMP-NEW", 50_000, 9, VoucherStatus.Issued, null),
                new VoucherInput("COMP-CHARGED", 40_000, 9, VoucherStatus.Redeemed, null),
                new VoucherInput("COMP-REVOKED", 30_000, 9, VoucherStatus.Revoked, InPeriod),
                new VoucherInput("COMP-REVOKED-LATE", 20_000, 9, VoucherStatus.Revoked, Cutoff.AddHours(1)),
                new VoucherInput("COMP-NEVER", 10_000, 9, VoucherStatus.Revoked, InPeriod),
            ],
            charges:
            [
                new VoucherChargeState("COMP-CHARGED", 1, 40_000, false),
                new VoucherChargeState("COMP-REVOKED", 2, 30_000, false),
                new VoucherChargeState("COMP-REVOKED-LATE", 3, 20_000, false),
            ]));

        Assert.Equal(2, result.VoucherAdjustments.Count);
        var charge = result.VoucherAdjustments.Single(v => v.VoucherCode == "COMP-NEW");
        Assert.Equal(-50_000, charge.Amount);
        Assert.False(charge.IsReversal);
        var reversal = result.VoucherAdjustments.Single(v => v.VoucherCode == "COMP-REVOKED");
        Assert.Equal(30_000, reversal.Amount);
        Assert.True(reversal.IsReversal);
        Assert.Equal(2, reversal.ReversesAdjustmentId);
        Assert.Equal(-20_000, result.Adjustment);
        Assert.Equal(-20_000, result.NetPayout);
        Assert.True(result.HasContent);
    }

    [Fact]
    public void AdjustmentsAndNegativeCarryOver_AreIncluded()
    {
        var result = SettlementCalculator.Compute(Input([Pay(1, 100_000)], [new PaymentAccrual(1, 0.10m, 10_000)],
            adjustments: [new AttachableAdjustment(5, 15_000), new AttachableAdjustment(6, -5_000)],
            carryOver: new CarryOverInput(3, -12_000)));

        Assert.Equal(10_000, result.Adjustment);
        Assert.Equal(-12_000, result.CarryOver);
        Assert.Equal(3, result.CarryOverFromSettlementId);
        Assert.Equal([5, 6], result.AttachedAdjustmentIds);
        Assert.Equal(100_000 - 10_000 + 10_000 - 12_000, result.NetPayout);
    }

    [Fact]
    public void PositiveCarryOver_IsIgnored()
    {
        var result = SettlementCalculator.Compute(Input(carryOver: new CarryOverInput(3, 5_000)));
        Assert.Equal(0, result.CarryOver);
        Assert.Null(result.CarryOverFromSettlementId);
        Assert.False(result.HasContent);
    }

    [Fact]
    public void OnlyCarryOver_StillHasContent()
        => Assert.True(SettlementCalculator.Compute(Input(carryOver: new CarryOverInput(3, -1))).HasContent);

    [Fact]
    public void EffectiveRate_ZeroGross_IsZero()
    {
        Assert.Equal(0, SettlementCalculator.EffectiveRate(-4_000, 0));
        Assert.Equal(9.9999m, SettlementCalculator.EffectiveRate(1_000_000, 1));
    }
}
