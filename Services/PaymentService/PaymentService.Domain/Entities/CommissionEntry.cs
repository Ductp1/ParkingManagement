using ParkingManagement.SharedKernel.Domain;

namespace PaymentService.Domain.Entities;

/// <summary>Loại bút toán hoa hồng nền tảng (US-079).</summary>
public enum CommissionEntryType
{
    /// <summary>Ghi nhận hoa hồng khi giao dịch đã thu tiền (một lần cho mỗi giao dịch).</summary>
    Accrual = 1,
    /// <summary>Giảm hoa hồng tương ứng phần tiền đã hoàn (số âm), có thể nhiều lần cho hoàn tiền từng phần.</summary>
    Reversal = 2,
}

/// <summary>
/// [PaymentService] Sổ hoa hồng nền tảng (US-079) – CHỈ THÊM, không sửa / xóa. Module: TV8.
/// Hoa hồng của một giao dịch = Σ Amount các bút toán của nó: Accrual = round(BaseAmount × Rate) ≥ 0, Reversal ≤ 0.
/// Rate là PHÂN SỐ (7,5% → 0.075), lấy từ AdminService theo thời điểm thu tiền và giữ nguyên cho mọi Reversal sau đó.
/// ParkingLotId / OwnerProfileId / PaidAtUtc chép từ Payment để báo cáo lọc thẳng trên bảng này (Payment không đổi schema).
/// RefundedAmountCovered = Payment.RefundedAmount đã được tính Reversal tới bút toán này (Accrual = 0) – khóa chống ghi trùng.
/// </summary>
public class CommissionEntry : BaseEntity
{
    public int PaymentId { get; private set; }
    /// <summary>→ ParkingService (không FK).</summary>
    public int ParkingLotId { get; private set; }
    /// <summary>→ UserService (không FK).</summary>
    public int OwnerProfileId { get; private set; }
    public CommissionEntryType EntryType { get; private set; }
    /// <summary>Accrual: số tiền giao dịch; Reversal: phần tiền hoàn mới (số âm).</summary>
    public decimal BaseAmount { get; private set; }
    public decimal Rate { get; private set; }
    public decimal Amount { get; private set; }
    /// <summary>LotContract | DefaultConfig (theo AdminService).</summary>
    public string RateSource { get; private set; } = string.Empty;
    public string? ContractNumber { get; private set; }
    public int? ContractVersion { get; private set; }
    public decimal RefundedAmountCovered { get; private set; }
    public DateTime PaidAtUtc { get; private set; }

    private CommissionEntry() { }

    public static CommissionEntry Accrual(int paymentId, int parkingLotId, int ownerProfileId, decimal paymentAmount, DateTime paidAtUtc,
        decimal rate, string rateSource, string? contractNumber, int? contractVersion, DateTime nowUtc)
    {
        if (paymentAmount < 0) throw new ArgumentOutOfRangeException(nameof(paymentAmount), "Số tiền giao dịch không được âm.");
        EnsureRate(rate);
        return new CommissionEntry
        {
            PaymentId = paymentId,
            ParkingLotId = parkingLotId,
            OwnerProfileId = ownerProfileId,
            EntryType = CommissionEntryType.Accrual,
            BaseAmount = paymentAmount,
            Rate = rate,
            Amount = RoundVnd(paymentAmount * rate),
            RateSource = rateSource,
            ContractNumber = contractNumber,
            ContractVersion = contractVersion,
            RefundedAmountCovered = 0,
            PaidAtUtc = paidAtUtc,
            CreatedAtUtc = nowUtc,
        };
    }

    /// <summary>
    /// Bù phần hoàn mới: tổng hoa hồng đã giảm = round(refundedAmount × rate) (tính trên lũy kế để hoàn từng phần không lệch làm tròn);
    /// bút toán này giảm phần còn thiếu so với <paramref name="reversedSoFar"/> (tổng |Amount| các Reversal trước).
    /// Hoàn toàn bộ → tổng hoa hồng của giao dịch về đúng 0.
    /// </summary>
    public static CommissionEntry Reversal(int paymentId, int parkingLotId, int ownerProfileId, DateTime paidAtUtc, decimal rate,
        string rateSource, string? contractNumber, int? contractVersion, decimal refundedAmount, decimal coveredSoFar, decimal reversedSoFar,
        DateTime nowUtc)
    {
        if (refundedAmount <= coveredSoFar)
            throw new InvalidOperationException($"Giao dịch {paymentId} không có phần hoàn mới cần bù hoa hồng.");
        if (reversedSoFar < 0) throw new ArgumentOutOfRangeException(nameof(reversedSoFar), "Tổng đã giảm phải là số dương.");
        EnsureRate(rate);
        var target = RoundVnd(refundedAmount * rate);
        return new CommissionEntry
        {
            PaymentId = paymentId,
            ParkingLotId = parkingLotId,
            OwnerProfileId = ownerProfileId,
            EntryType = CommissionEntryType.Reversal,
            BaseAmount = -(refundedAmount - coveredSoFar),
            Rate = rate,
            Amount = -Math.Max(0, target - reversedSoFar),
            RateSource = rateSource,
            ContractNumber = contractNumber,
            ContractVersion = contractVersion,
            RefundedAmountCovered = refundedAmount,
            PaidAtUtc = paidAtUtc,
            CreatedAtUtc = nowUtc,
        };
    }

    /// <summary>VND không có phần lẻ, làm tròn nửa lên (AwayFromZero) – từng giao dịch.</summary>
    public static decimal RoundVnd(decimal amount) => Math.Round(amount, 0, MidpointRounding.AwayFromZero);

    private static void EnsureRate(decimal rate)
    {
        if (rate is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(rate), "Tỷ lệ hoa hồng phải là phân số 0–1.");
    }
}
