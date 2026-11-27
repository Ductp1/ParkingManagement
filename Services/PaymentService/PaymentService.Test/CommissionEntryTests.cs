using PaymentService.Domain.Entities;

namespace PaymentService.Test;

// US-079: tính tiền của một bút toán hoa hồng – làm tròn VND từng giao dịch, Reversal theo lũy kế để hoàn từng phần không lệch.
public class CommissionEntryTests
{
    private static readonly DateTime PaidAt = new(2026, 11, 5, 3, 0, 0, DateTimeKind.Utc);

    private static CommissionEntry Accrual(decimal amount, decimal rate)
        => CommissionEntry.Accrual(1, 11, 1, amount, PaidAt, rate, "LotContract", "HD-SP-2026-0011", 2, PaidAt);

    private static CommissionEntry Reversal(decimal rate, decimal refunded, decimal covered, decimal reversed)
        => CommissionEntry.Reversal(1, 11, 1, PaidAt, rate, "LotContract", "HD-SP-2026-0011", 2, refunded, covered, reversed, PaidAt);

    [Theory]
    [InlineData(12_345, 0.075, 926)]        // 925.875 → 926
    [InlineData(10_010, 0.05, 501)]         // 500.5 → 501 (nửa lên, không làm tròn chẵn)
    [InlineData(10_000, 0.1, 1_000)]
    [InlineData(0, 0.1, 0)]
    [InlineData(55_555, 0, 0)]
    public void Accrual_RoundsEachTransactionToWholeVnd_AwayFromZero(decimal amount, decimal rate, decimal expected)
    {
        var entry = Accrual(amount, rate);

        Assert.Equal(expected, entry.Amount);
        Assert.Equal((CommissionEntryType.Accrual, amount, rate, 0m), (entry.EntryType, entry.BaseAmount, entry.Rate, entry.RefundedAmountCovered));
        Assert.Equal(("LotContract", "HD-SP-2026-0011", (int?)2), (entry.RateSource, entry.ContractNumber, entry.ContractVersion));
        Assert.Equal(PaidAt, entry.PaidAtUtc);
    }

    [Fact]
    public void PartialRefunds_ReverseOnCumulativeAmount_AndFullRefundEndsAtZero()
    {
        const decimal rate = 0.075m;
        var accrual = Accrual(12_345, rate);                                   // 926
        var first = Reversal(rate, refunded: 3_333, covered: 0, reversed: 0);  // round(249.975) = 250
        var second = Reversal(rate, refunded: 6_667, covered: 3_333, reversed: 250);   // round(500.025) = 500 → thêm 250
        var last = Reversal(rate, refunded: 12_345, covered: 6_667, reversed: 500);    // 926 → thêm 426

        Assert.Equal([-250m, -250m, -426m], new[] { first.Amount, second.Amount, last.Amount });
        Assert.Equal([-3_333m, -3_334m, -5_678m], new[] { first.BaseAmount, second.BaseAmount, last.BaseAmount });
        Assert.Equal([3_333m, 6_667m, 12_345m], new[] { first.RefundedAmountCovered, second.RefundedAmountCovered, last.RefundedAmountCovered });
        Assert.Equal(0, accrual.Amount + first.Amount + second.Amount + last.Amount);
    }

    [Fact]
    public void TinyRefund_RoundingToZero_StillRecordsCoverage()
    {
        var entry = Reversal(0.05m, refunded: 9, covered: 0, reversed: 0);     // 0.45 → 0
        Assert.Equal((0m, -9m, 9m), (entry.Amount, entry.BaseAmount, entry.RefundedAmountCovered));
    }

    [Fact]
    public void Reversal_WithoutNewRefund_Throws()
        => Assert.Throws<InvalidOperationException>(() => Reversal(0.1m, refunded: 5_000, covered: 5_000, reversed: 500));

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(7.5)]                        // % chưa đổi sang phân số
    public void Rate_MustBeFraction(decimal rate)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Accrual(10_000, rate));
}
