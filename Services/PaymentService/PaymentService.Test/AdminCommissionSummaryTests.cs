using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Commission;
using PaymentService.Domain.Entities;
using static PaymentService.Test.OwnerRevenueTestData;

namespace PaymentService.Test;

// US-079: tổng hợp hoa hồng toàn sàn cho Admin – theo bãi / chủ bãi / nguồn tỷ lệ / ngày (giờ Việt Nam), không có dữ liệu từng khách.
public class AdminCommissionSummaryTests
{
    private static readonly DateTime From = Utc(2026, 9, 30, 17);    // 01/10 00:00 ICT
    private static readonly DateTime To = Utc(2026, 10, 3, 17);      // 04/10 00:00 ICT

    private static readonly Payment A1 = Paid(OwnerA, LotA1, 100_000, Utc(2026, 10, 1, 2)).WithId(1);
    private static readonly Payment A1Refunded = Paid(OwnerA, LotA1, 50_000, Utc(2026, 10, 1, 18), status: PaymentStatus.PartiallyRefunded,
        refunded: 20_000).WithId(2);                                                    // 02/10 01:00 ICT
    private static readonly Payment A2 = Paid(OwnerA, LotA2, 40_000, Utc(2026, 10, 2, 2)).WithId(3);
    private static readonly Payment B1Pending = Paid(OwnerB, LotB1, 60_000, Utc(2026, 10, 3, 2)).WithId(4);
    private static readonly Payment OutOfRange = Paid(OwnerB, LotB1, 999_000, Utc(2026, 10, 3, 17)).WithId(5);
    private static readonly Payment Failed = Paid(OwnerB, LotB1, 888_000, Utc(2026, 10, 2, 2), status: PaymentStatus.Failed).WithId(6);

    private static GetAdminCommissionSummaryUseCase UseCase(CommissionPolicy? policy = null)
        => new(new InMemoryAdminCommissionQueries([A1, A1Refunded, A2, B1Pending, OutOfRange, Failed],
        [
            Accrual(A1, 0.075m, "LotContract", "HD-SP-2026-0011"),                   // 7.500
            Accrual(A1Refunded, 0.075m, "LotContract", "HD-SP-2026-0011"),           // 3.750
            Reversal(A1Refunded, 0.075m, 20_000, source: "LotContract"),             // −1.500
            Accrual(A2, 0.10m),                                                      // 4.000
            Accrual(OutOfRange, 0.10m),
        ]), new FixedTimeProvider(Utc(2026, 11, 6)), policy);

    [Fact]
    public async Task Totals_AndEffectiveRate()
    {
        var summary = await UseCase().ExecuteAsync(From, To);

        Assert.Equal(new AdminCommissionTotalsDto(250_000, 20_000, 230_000, 13_750, Math.Round(13_750m / 230_000m, 6), 4, 1, "Partial"),
            summary.Totals);
        Assert.Equal(("VND", From, To), (summary.Currency, summary.FromUtc, summary.ToUtc));
    }

    [Fact]
    public async Task ByLot_And_ByOwner()
    {
        var summary = await UseCase().ExecuteAsync(From, To);

        var lotA1 = summary.ByLot[0];
        Assert.Equal((LotA1, OwnerA, 150_000m, 20_000m, 130_000m, 9_750m, 0.075m, 2, 0, "Calculated"),
            (lotA1.ParkingLotId, lotA1.OwnerProfileId, lotA1.GrossAmount, lotA1.RefundedAmount, lotA1.NetAmount, lotA1.CommissionAmount,
                lotA1.EffectiveRate, lotA1.TransactionCount, lotA1.PendingCount, lotA1.Status));
        Assert.Equal(["LotContract"], lotA1.RateSources);
        var lotB1 = summary.ByLot.Single(l => l.ParkingLotId == LotB1);
        Assert.Equal((0m, 1, "Pending", 0.0m), (lotB1.CommissionAmount, lotB1.PendingCount, lotB1.Status, lotB1.EffectiveRate!.Value));
        Assert.Empty(lotB1.RateSources);

        Assert.Equal([OwnerA, OwnerB], summary.ByOwner.Select(o => o.OwnerProfileId));
        Assert.Equal((2, 190_000m, 13_750m, 0), (summary.ByOwner[0].LotCount, summary.ByOwner[0].GrossAmount, summary.ByOwner[0].CommissionAmount,
            summary.ByOwner[0].PendingCount));
    }

    [Fact]
    public async Task ByRateSource_SplitsContractAndDefault()
    {
        var summary = await UseCase().ExecuteAsync(From, To);

        Assert.Equal(
        [
            new AdminCommissionByRateSourceDto("DefaultConfig", 1, 40_000, 0, 4_000, 0.1m),
            new AdminCommissionByRateSourceDto("LotContract", 2, 150_000, 20_000, 9_750, 0.075m),
        ], summary.ByRateSource);
    }

    [Fact]
    public async Task Series_HasEveryVietnamDay()
    {
        var summary = await UseCase().ExecuteAsync(From, To);

        Assert.Equal([new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 3)], summary.Series.Select(d => d.Date));
        Assert.Equal([7_500m, 6_250m, 0m], summary.Series.Select(d => d.CommissionAmount));
        Assert.Equal([0, 0, 1], summary.Series.Select(d => d.PendingCount));
        Assert.Equal([100_000m, 70_000m, 60_000m], summary.Series.Select(d => d.NetAmount));
    }

    [Fact]
    public async Task ExcludedPurpose_IsNotPending()
    {
        var summary = await UseCase(new CommissionPolicy([PaymentPurpose.Booking])).ExecuteAsync(From, To);
        Assert.Equal(0, summary.Totals.PendingCount);
    }

    [Fact]
    public async Task EmptyRange_ReturnsZerosWithNullEffectiveRate()
    {
        var summary = await UseCase().ExecuteAsync(Utc(2024, 12, 31, 17), Utc(2025, 1, 1, 17));      // đúng một ngày ICT
        Assert.Equal(new AdminCommissionTotalsDto(0, 0, 0, 0, null, 0, 0, "Calculated"), summary.Totals);
        Assert.Empty(summary.ByLot);
        Assert.Single(summary.Series);
    }

    public static TheoryData<DateTime?, DateTime?> InvalidRanges() => new()
    {
        { null, To },
        { From, null },
        { To, From },
        { From, From },
        { From, From.AddDays(367) },
    };

    [Theory]
    [MemberData(nameof(InvalidRanges))]
    public async Task InvalidRange_Throws400(DateTime? from, DateTime? to)
        => await Assert.ThrowsAsync<ValidationException>(() => UseCase().ExecuteAsync(from, to));

    [Fact]
    public async Task Range366Days_IsAllowed()
    {
        var summary = await UseCase().ExecuteAsync(From, From.AddDays(366));
        Assert.Equal(366, summary.Series.Count);
    }
}
