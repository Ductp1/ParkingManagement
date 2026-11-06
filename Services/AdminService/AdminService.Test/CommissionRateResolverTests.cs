using AdminService.Application.Features.Commission;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-079: quy tắc chọn tỷ lệ hoa hồng (hàm thuần). Ngày hiệu lực theo giờ Việt Nam (UTC+7), hai đầu đều tính.
public class CommissionRateResolverTests
{
    private const decimal Default = 0.10m;

    internal static SignedLotContractRate Contract(int id = 1, int lotId = 7, decimal percent = 7.5m, DateOnly? from = null, DateOnly? to = null,
        DateTime? signedAtUtc = null, int version = 2, string? number = null)
        => new(id, number ?? $"HD-SP-2026-{id:0000}", lotId, OwnerProfileId: 30, BusinessName: "Công ty Bãi Xanh", LotName: "Bãi Xanh Ba Đình",
            LotDistrict: "Ba Đình", version, percent, from, to, signedAtUtc ?? new DateTime(2026, 9, 30, 3, 0, 0, DateTimeKind.Utc));

    private static readonly DateOnly From = new(2026, 10, 1);
    private static readonly DateOnly To = new(2027, 9, 30);

    /// <summary>00:00 giờ Việt Nam của <paramref name="day"/>, đổi sang UTC.</summary>
    private static DateTime IctMidnightUtc(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);

    [Fact]
    public void ContractInEffect_ConvertsPercentToFraction()
    {
        var result = CommissionRateResolver.Resolve([Contract(percent: 7.5m, from: From, to: To)], Default,
            new DateTime(2026, 11, 5, 3, 0, 0, DateTimeKind.Utc));

        Assert.Equal(0.075m, result.Rate);
        Assert.Equal(CommissionRateSources.LotContract, result.Source);
        Assert.Null(result.Reason);
        Assert.Equal("HD-SP-2026-0001", result.Contract!.ContractNumber);
        Assert.Equal((From, (DateOnly?)To), (result.EffectiveFrom!.Value, result.EffectiveTo));
    }

    [Fact]
    public void EffectiveFrom_IsInclusive_InVietnamTime()
    {
        var contracts = new[] { Contract(from: From, to: To) };

        var firstMoment = CommissionRateResolver.Resolve(contracts, Default, IctMidnightUtc(From));      // 30/09 17:00 UTC = 01/10 00:00 ICT
        var justBefore = CommissionRateResolver.Resolve(contracts, Default, IctMidnightUtc(From).AddSeconds(-1));

        Assert.Equal(CommissionRateSources.LotContract, firstMoment.Source);
        Assert.Equal(CommissionRateSources.DefaultConfig, justBefore.Source);
        Assert.Equal(CommissionRateReasons.BeforeEffective, justBefore.Reason);
        Assert.Equal(Default, justBefore.Rate);
        Assert.NotNull(justBefore.Contract);                                                             // vẫn trả hợp đồng để hiển thị / liên kết
    }

    [Fact]
    public void EffectiveTo_IsInclusive_InVietnamTime()
    {
        var contracts = new[] { Contract(from: From, to: To) };
        var nextDay = IctMidnightUtc(To.AddDays(1));

        var lastMoment = CommissionRateResolver.Resolve(contracts, Default, nextDay.AddSeconds(-1));
        var expired = CommissionRateResolver.Resolve(contracts, Default, nextDay);

        Assert.Equal(CommissionRateSources.LotContract, lastMoment.Source);
        Assert.Equal(CommissionRateSources.DefaultConfig, expired.Source);
        Assert.Equal(CommissionRateReasons.AfterExpiry, expired.Reason);
    }

    [Fact]
    public void NoSignedContract_UsesDefault()
    {
        var result = CommissionRateResolver.Resolve([], 0.12m, new DateTime(2026, 11, 5, 3, 0, 0, DateTimeKind.Utc));

        Assert.Equal(0.12m, result.Rate);
        Assert.Equal(CommissionRateSources.DefaultConfig, result.Source);
        Assert.Equal(CommissionRateReasons.NoSignedContract, result.Reason);
        Assert.Null(result.Contract);
    }

    [Fact]
    public void DocumentContract_WithoutDates_StartsOnSigningDayInVietnamTime_AndNeverExpires()
    {
        // Ký 05/11 18:00 UTC = 06/11 01:00 ICT → hiệu lực từ 06/11 (ICT), không có ngày kết thúc.
        var contracts = new[] { Contract(percent: 5m, signedAtUtc: new DateTime(2026, 11, 5, 18, 0, 0, DateTimeKind.Utc)) };

        var sameIctDay = CommissionRateResolver.Resolve(contracts, Default, new DateTime(2026, 11, 5, 17, 30, 0, DateTimeKind.Utc));
        var dayBefore = CommissionRateResolver.Resolve(contracts, Default, new DateTime(2026, 11, 5, 16, 59, 0, DateTimeKind.Utc));
        var farFuture = CommissionRateResolver.Resolve(contracts, Default, new DateTime(2035, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(0.05m, sameIctDay.Rate);
        Assert.Equal(new DateOnly(2026, 11, 6), sameIctDay.EffectiveFrom);
        Assert.Null(sameIctDay.EffectiveTo);
        Assert.Equal(CommissionRateReasons.BeforeEffective, dayBefore.Reason);
        Assert.Equal(CommissionRateSources.LotContract, farFuture.Source);
    }

    [Fact]
    public void OverlappingSignedContracts_UseLatestSigned_AndFlagOverlap()
    {
        var older = Contract(id: 1, percent: 7m, from: From, to: To, signedAtUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var newer = Contract(id: 2, percent: 6m, from: From, to: To, signedAtUtc: new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc));

        var result = CommissionRateResolver.Resolve([older, newer], Default, new DateTime(2026, 11, 5, 3, 0, 0, DateTimeKind.Utc));

        Assert.Equal(0.06m, result.Rate);
        Assert.Equal(2, result.Contract!.ContractId);
        Assert.True(result.OverlappingContracts);
    }

    [Fact]
    public void ExpiredContract_DoesNotHideAnotherContractInEffect()
    {
        var expired = Contract(id: 1, percent: 9m, from: new DateOnly(2025, 1, 1), to: new DateOnly(2025, 12, 31),
            signedAtUtc: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        var current = Contract(id: 2, percent: 6m, from: From, to: To, signedAtUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = CommissionRateResolver.Resolve([expired, current], Default, new DateTime(2026, 11, 5, 3, 0, 0, DateTimeKind.Utc));

        Assert.Equal(0.06m, result.Rate);
        Assert.False(result.OverlappingContracts);
    }

    [Theory]
    [InlineData("0.10", 0.10)]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("0.075", 0.075)]
    public void ParseDefaultRate_AcceptsFractionBetweenZeroAndOne(string value, decimal expected)
        => Assert.Equal(expected, CommissionRateResolver.ParseDefaultRate(value));

    [Theory]
    [InlineData("10")]
    [InlineData("-0.1")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseDefaultRate_Invalid_Throws503InsteadOfGuessing(string? value)
        => Assert.Throws<DependencyUnavailableException>(() => CommissionRateResolver.ParseDefaultRate(value));
}
