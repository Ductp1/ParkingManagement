using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features.Commission;
using PaymentService.Domain.Entities;
using static PaymentService.Test.OwnerRevenueTestData;

namespace PaymentService.Test;

// US-079: một lượt của job tính hoa hồng – Accrual theo tỷ lệ AdminService, Reversal theo tiền hoàn, không đoán tỷ lệ khi AdminService lỗi,
// chạy lại / chạy song song không ghi trùng.
public class CommissionAccrualTests
{
    private static readonly DateTime Now = Utc(2026, 11, 6, 3);

    private static RunCommissionAccrualUseCase UseCase(InMemoryCommissionLedger ledger, FakeCommissionRateClient rates,
        CommissionPolicy? policy = null)
        => new(ledger, rates, new FixedTimeProvider(Now), NullLogger<RunCommissionAccrualUseCase>.Instance, policy);

    [Fact]
    public async Task Accrual_UsesRateResolvedAtPaidAt_AndKeepsContractOnlyForContractRates()
    {
        var contractLot = Paid(OwnerA, LotA1, 12_345, Utc(2026, 11, 5, 3)).WithId(1);
        var defaultLot = Paid(OwnerA, LotA2, 40_000, Utc(2026, 11, 5, 4)).WithId(2);
        var ledger = new InMemoryCommissionLedger([contractLot, defaultLot]);
        var rates = new FakeCommissionRateClient { Lots = { [LotA1] = (0.075m, "LotContract", "HD-SP-2026-0011", 2) } };

        var result = await UseCase(ledger, rates).ExecuteAsync();

        Assert.Equal((2, 2, 0, null as string), (result.AccrualCandidates, result.Accrued, result.Reversed, result.RateError));
        Assert.Equal([(LotA1, Utc(2026, 11, 5, 3)), (LotA2, Utc(2026, 11, 5, 4))], Assert.Single(rates.Requests));
        var first = ledger.Entries.Single(e => e.PaymentId == 1);
        Assert.Equal((926m, 0.075m, "LotContract", "HD-SP-2026-0011", (int?)2),
            (first.Amount, first.Rate, first.RateSource, first.ContractNumber, first.ContractVersion));
        Assert.Equal((LotA1, OwnerA, Utc(2026, 11, 5, 3), Now), (first.ParkingLotId, first.OwnerProfileId, first.PaidAtUtc, first.CreatedAtUtc));
        var second = ledger.Entries.Single(e => e.PaymentId == 2);
        Assert.Equal((4_000m, "DefaultConfig", null as string, null as int?),
            (second.Amount, second.RateSource, second.ContractNumber, second.ContractVersion));
        Assert.Equal(1, ledger.LocksReleased);
    }

    [Fact]
    public async Task Accrual_SameLotAndInstant_IsAskedOnce()
    {
        var at = Utc(2026, 11, 5, 3);
        var ledger = new InMemoryCommissionLedger([Paid(OwnerA, LotA1, 1_000, at).WithId(1), Paid(OwnerA, LotA1, 2_000, at).WithId(2)]);
        var rates = new FakeCommissionRateClient();

        await UseCase(ledger, rates).ExecuteAsync();

        Assert.Single(Assert.Single(rates.Requests));
        Assert.Equal(2, ledger.Entries.Count);
    }

    [Theory]
    [InlineData(PaymentStatus.Succeeded, true)]
    [InlineData(PaymentStatus.PartiallyRefunded, true)]
    [InlineData(PaymentStatus.Refunded, true)]
    [InlineData(PaymentStatus.Pending, false)]
    [InlineData(PaymentStatus.Failed, false)]
    [InlineData(PaymentStatus.Cancelled, false)]
    public async Task Accrual_OnlyForCollectedPayments(PaymentStatus status, bool accrued)
    {
        var ledger = new InMemoryCommissionLedger([Paid(OwnerA, LotA1, 10_000, Utc(2026, 11, 5, 3), status: status,
            refunded: status == PaymentStatus.Refunded ? 10_000 : 0).WithId(1)]);

        await UseCase(ledger, new FakeCommissionRateClient()).ExecuteAsync();

        Assert.Equal(accrued, ledger.Entries.Any(e => e.EntryType == CommissionEntryType.Accrual));
    }

    [Fact]
    public async Task Accrual_SkipsPaymentWithoutPaidAt()
    {
        var payment = Paid(OwnerA, LotA1, 10_000, Utc(2026, 11, 5, 3)).WithId(1);
        payment.PaidAtUtc = null;
        var ledger = new InMemoryCommissionLedger([payment]);

        var result = await UseCase(ledger, new FakeCommissionRateClient()).ExecuteAsync();

        Assert.Equal(0, result.AccrualCandidates);
        Assert.Empty(ledger.Entries);
    }

    [Fact]
    public async Task AllFivePurposes_AreCommissionedByDefault_AndExcludedPurposesAreSkipped()
    {
        Payment[] payments = [.. Enum.GetValues<PaymentPurpose>().Select((purpose, i) =>
            Paid(OwnerA, LotA1, 10_000, Utc(2026, 11, 5, 3 + i), purpose: purpose).WithId(i + 1))];

        var all = new InMemoryCommissionLedger(payments);
        await UseCase(all, new FakeCommissionRateClient()).ExecuteAsync();
        var withoutOverstay = new InMemoryCommissionLedger(payments);
        await UseCase(withoutOverstay, new FakeCommissionRateClient(), new CommissionPolicy([PaymentPurpose.Overstay])).ExecuteAsync();

        Assert.Equal(5, all.Entries.Count);
        Assert.Equal(4, withoutOverstay.Entries.Count);
        Assert.DoesNotContain(withoutOverstay.Entries, e => payments.Single(p => p.Id == e.PaymentId).Purpose == PaymentPurpose.Overstay);
    }

    [Fact]
    public async Task AdminServiceDown_WritesNoAccrual_ButReversalStillRuns()
    {
        var refunded = Paid(OwnerA, LotA1, 50_000, Utc(2026, 11, 4, 3), status: PaymentStatus.PartiallyRefunded, refunded: 20_000).WithId(1);
        var fresh = Paid(OwnerA, LotA2, 30_000, Utc(2026, 11, 5, 3)).WithId(2);
        var ledger = new InMemoryCommissionLedger([refunded, fresh], [Accrual(refunded, 0.1m)]);
        var rates = new FakeCommissionRateClient { Failure = new CommissionRateUnavailableException("AdminService không phản hồi trong 5 giây.") };

        var result = await UseCase(ledger, rates).ExecuteAsync();

        Assert.Equal((1, 0, 1), (result.AccrualCandidates, result.Accrued, result.Reversed));
        Assert.Contains("không phản hồi", result.RateError);
        Assert.DoesNotContain(ledger.Entries, e => e.PaymentId == 2);              // không đoán tỷ lệ
        Assert.Equal(3_000m, ledger.CommissionOf(1));                              // 5.000 − 2.000

        rates.Failure = null;                                                       // AdminService sống lại → lượt sau ghi bù
        await UseCase(ledger, rates).ExecuteAsync();
        Assert.Equal(3_000m, ledger.CommissionOf(2));
    }

    public static TheoryData<string> BadQuotes() => ["count", "rate", "lot"];

    [Theory]
    [MemberData(nameof(BadQuotes))]
    public async Task InvalidAdminResponse_WritesNothing(string defect)
    {
        var ledger = new InMemoryCommissionLedger([Paid(OwnerA, LotA1, 10_000, Utc(2026, 11, 5, 3)).WithId(1),
            Paid(OwnerA, LotA2, 10_000, Utc(2026, 11, 5, 4)).WithId(2)]);
        var rates = new FakeCommissionRateClient
        {
            Tamper = quotes => defect switch
            {
                "count" => [quotes[0]],
                "rate" => [quotes[0] with { Rate = 7.5m }, quotes[1]],                // % chưa đổi sang phân số
                _ => [quotes[1], quotes[0]],                                          // sai thứ tự / sai bãi
            },
        };

        var result = await UseCase(ledger, rates).ExecuteAsync();

        Assert.Empty(ledger.Entries);
        Assert.NotNull(result.RateError);
    }

    [Fact]
    public async Task SecondRun_IsIdempotent()
    {
        var payment = Paid(OwnerA, LotA1, 50_000, Utc(2026, 11, 5, 3), status: PaymentStatus.PartiallyRefunded, refunded: 10_000).WithId(1);
        var ledger = new InMemoryCommissionLedger([payment]);
        var rates = new FakeCommissionRateClient();

        var first = await UseCase(ledger, rates).ExecuteAsync();
        var second = await UseCase(ledger, rates).ExecuteAsync();

        Assert.Equal((1, 1), (first.Accrued, first.Reversed));                      // Accrual rồi Reversal ngay trong cùng lượt
        Assert.Equal((0, 0, 0), (second.AccrualCandidates, second.ReversalCandidates, second.Processed));
        Assert.Single(rates.Requests);
        Assert.Equal(4_000m, ledger.CommissionOf(1));
    }

    [Fact]
    public async Task ConcurrentInstanceWroteFirst_DuplicateIsSkipped_NotThrown()
    {
        var payment = Paid(OwnerA, LotA1, 20_000, Utc(2026, 11, 5, 3)).WithId(1);
        var ledger = new InMemoryCommissionLedger([payment])
        {
            BeforeAdd = l => l.TryInsert(Accrual(payment, 0.1m)),                   // instance khác ghi chen giữa lúc đọc và lúc ghi
        };

        var result = await UseCase(ledger, new FakeCommissionRateClient()).ExecuteAsync();

        Assert.Equal((0, 1), (result.Accrued, result.Duplicates));
        Assert.Single(ledger.Entries);
    }

    [Fact]
    public async Task LockHeldByAnotherInstance_SkipsRun()
    {
        var ledger = new InMemoryCommissionLedger([Paid(OwnerA, LotA1, 20_000, Utc(2026, 11, 5, 3)).WithId(1)]) { LockAvailable = false };
        var rates = new FakeCommissionRateClient();

        var result = await UseCase(ledger, rates).ExecuteAsync();

        Assert.True(result.Skipped);
        Assert.Empty(rates.Requests);
        Assert.Empty(ledger.Entries);
    }

    [Fact]
    public async Task PartialRefundsOverSeveralRuns_ReverseProportionally_AndFullRefundNetsZero()
    {
        var payment = Paid(OwnerA, LotA1, 12_345, Utc(2026, 11, 5, 3)).WithId(1);
        var ledger = new InMemoryCommissionLedger([payment]);
        var rates = new FakeCommissionRateClient { Lots = { [LotA1] = (0.075m, "LotContract", "HD-SP-2026-0011", 1) } };
        var useCase = UseCase(ledger, rates);

        await useCase.ExecuteAsync();                                               // 926
        (payment.RefundedAmount, payment.Status) = (3_333, PaymentStatus.PartiallyRefunded);
        await useCase.ExecuteAsync();                                               // −250
        (payment.RefundedAmount, payment.Status) = (12_345, PaymentStatus.Refunded);
        await useCase.ExecuteAsync();                                               // −676

        var reversals = ledger.Entries.Where(e => e.EntryType == CommissionEntryType.Reversal).ToList();
        Assert.Equal([-250m, -676m], reversals.Select(r => r.Amount));
        Assert.All(reversals, r => Assert.Equal((0.075m, "LotContract", "HD-SP-2026-0011"), (r.Rate, r.RateSource, r.ContractNumber)));
        Assert.Equal(0, ledger.CommissionOf(1));
        Assert.Single(rates.Requests);                                              // Reversal không gọi AdminService
    }

    [Fact]
    public async Task BatchSize_LimitsOneRun_OldestFirst()
    {
        Payment[] payments = [.. Enumerable.Range(1, 5).Select(i => Paid(OwnerA, LotA1, 1_000, Utc(2026, 11, 6 - i, 3)).WithId(i))];
        var ledger = new InMemoryCommissionLedger(payments);

        var result = await UseCase(ledger, new FakeCommissionRateClient(), new CommissionPolicy([], BatchSize: 2)).ExecuteAsync();

        Assert.Equal(2, result.Accrued);
        Assert.Equal([5, 4], ledger.Entries.Select(e => e.PaymentId));            // PaidAtUtc cũ nhất trước
    }
}
