using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Services;

namespace PaymentService.Test;

/// <summary>Bảng giá Vincom: giờ đầu 30.000đ, giờ 2–4 20.000đ/giờ, từ giờ thứ 5 15.000đ/giờ.</summary>
public class PriceCalculatorTests
{
    // Thứ Sáu 02/10/2026 09:00 giờ VN = 02:00 UTC (ngày thường, không nhân hệ số cuối tuần)
    private static readonly DateTime Weekday = new(2026, 10, 2, 2, 0, 0, DateTimeKind.Utc);

    private static RateCard VincomCard()
    {
        var card = new RateCard { WeekendMultiplier = 1.2m, OversizedMultiplier = 1.5m, MaxDailyAmount = 300000 };
        foreach (var t in new[] { VehicleType.Sedan, VehicleType.Suv })
        {
            card.Rules.Add(new RateRule { VehicleType = t, FromMinute = 0, ToMinute = 60, BlockMinutes = 60, PricePerBlock = 30000 });
            card.Rules.Add(new RateRule { VehicleType = t, FromMinute = 60, ToMinute = 240, BlockMinutes = 60, PricePerBlock = 20000 });
            card.Rules.Add(new RateRule { VehicleType = t, FromMinute = 240, ToMinute = null, BlockMinutes = 60, PricePerBlock = 15000 });
        }
        return card;
    }

    [Fact] // TC-PARK-07: vào và ra trong 14 phút → 0đ
    public void Within_15_minute_grace_period_is_free()
    {
        var q = PriceCalculator.Calculate(VincomCard(), VehicleType.Sedan, Weekday, Weekday.AddMinutes(14));
        Assert.True(q.IsWithinGracePeriod);
        Assert.Equal(0, q.FinalAmount);
    }

    [Theory]
    [InlineData(16, 30000)]    // quá ân hạn → tính block đầu
    [InlineData(60, 30000)]
    [InlineData(61, 50000)]    // block lẻ làm tròn lên
    [InlineData(120, 50000)]   // = BK-0001 trong dữ liệu demo
    [InlineData(180, 70000)]   // = BK-0002 trước giảm giá
    [InlineData(300, 105000)]  // 30k + 3×20k + 15k
    public void Tiered_blocks_are_summed(int minutes, decimal expected)
        => Assert.Equal(expected, PriceCalculator.Calculate(VincomCard(), VehicleType.Sedan, Weekday, Weekday.AddMinutes(minutes)).FinalAmount);

    [Fact]
    public void Weekend_multiplier_applies_by_vietnam_time()
    {
        var saturday = new DateTime(2026, 10, 3, 2, 0, 0, DateTimeKind.Utc); // 09:00 thứ Bảy giờ VN
        Assert.Equal(60000, PriceCalculator.Calculate(VincomCard(), VehicleType.Sedan, saturday, saturday.AddMinutes(120)).FinalAmount);
    }

    [Fact]
    public void Oversized_vehicle_without_own_rules_uses_suv_price_times_multiplier()
        => Assert.Equal(75000, PriceCalculator.Calculate(VincomCard(), VehicleType.Oversized, Weekday, Weekday.AddMinutes(120)).FinalAmount);

    [Fact]
    public void Daily_cap_is_respected()
        => Assert.Equal(300000, PriceCalculator.Calculate(VincomCard(), VehicleType.Sedan, Weekday, Weekday.AddHours(23)).FinalAmount);
}
