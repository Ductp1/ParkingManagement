using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Services;
using Xunit;

namespace PaymentService.Test;

/// <summary>
/// Bộ kiểm thử tự động toàn diện cho công cụ tính giá PriceCalculator (US-070, US-071).
/// </summary>
public class PriceCalculatorTests
{
    // Thứ Sáu 02/10/2026 09:00 giờ VN = 02:00 UTC (ngày thường, không nhân hệ số cuối tuần)
    private static readonly DateTime Weekday = new(2026, 10, 2, 2, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Biểu giá Vincom: giờ đầu 30.000đ, giờ 2–4 20.000đ/giờ, từ giờ thứ 5 15.000đ/giờ.
    /// Hệ số cuối tuần: 1.2, Hệ số ngày lễ: 1.3, Phụ thu qua đêm: 50.000đ, Hệ số xe quá khổ: 1.5, Trần ngày: 300.000đ.
    /// </summary>
    private static RateCard VincomCard()
    {
        var card = new RateCard 
        { 
            WeekendMultiplier = 1.2m, 
            HolidayMultiplier = 1.3m,
            OversizedMultiplier = 1.5m, 
            OvernightSurcharge = 50000m,
            MaxDailyAmount = 300000m 
        };

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

    [Fact] // TC-PARK-07: vào và ra đúng mốc 15 phút tròn → 0đ (vẫn trong ân hạn miễn phí)
    public void Exactly_15_minute_grace_period_is_free()
    {
        var q = PriceCalculator.Calculate(VincomCard(), VehicleType.Sedan, Weekday, Weekday.AddMinutes(15));
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
    public void Holiday_multiplier_applies_when_date_is_in_holiday_list()
    {
        // Thứ Tư 02/09/2026 lúc 09:00 sáng giờ VN (02:00 UTC)
        var nationalDay = new DateTime(2026, 9, 2, 2, 0, 0, DateTimeKind.Utc);
        var holidays = new HashSet<DateOnly> { new(2026, 9, 2) };

        // Đỗ 120 phút: giá gốc 50.000đ x hệ số ngày lễ 1.3 = 65.000đ
        var quote = PriceCalculator.Calculate(VincomCard(), VehicleType.Sedan, nationalDay, nationalDay.AddMinutes(120), holidays);
        Assert.Equal(65000, quote.FinalAmount);
        Assert.Equal(1.3m, quote.Multiplier);
    }

    [Fact]
    public void Overnight_surcharge_applies_when_parking_crosses_night_window()
    {
        // Thứ Sáu 20:00 VN (13:00 UTC) đến 23:00 VN (16:00 UTC) -> 180 phút
        var entryUtc = new DateTime(2026, 10, 2, 13, 0, 0, DateTimeKind.Utc);
        var exitUtc = entryUtc.AddHours(3);

        // 70.000đ (3 tiếng) + 50.000đ (phụ thu qua đêm 22:00–06:00) = 120.000đ
        var quote = PriceCalculator.Calculate(VincomCard(), VehicleType.Sedan, entryUtc, exitUtc);
        Assert.Equal(120000, quote.FinalAmount);
        Assert.Contains(quote.Lines, l => l.Description.Contains("Phụ thu qua đêm"));
    }

    [Fact]
    public void Daytime_parking_does_not_incur_overnight_surcharge()
    {
        // Thứ Sáu 08:00 VN (01:00 UTC) đến 18:00 VN (11:00 UTC) -> hoàn toàn ban ngày
        var entryUtc = new DateTime(2026, 10, 2, 1, 0, 0, DateTimeKind.Utc);
        var exitUtc = entryUtc.AddHours(10);

        var quote = PriceCalculator.Calculate(VincomCard(), VehicleType.Sedan, entryUtc, exitUtc);
        Assert.DoesNotContain(quote.Lines, l => l.Description.Contains("Phụ thu qua đêm"));
    }

    [Fact]
    public void Holiday_takes_priority_over_weekend_multiplier()
    {
        // Thứ Bảy ngày lễ 03/10/2026: Ưu tiên hệ số lễ 1.3 thay vì cuối tuần 1.2
        var saturdayHoliday = new DateTime(2026, 10, 3, 2, 0, 0, DateTimeKind.Utc);
        var holidays = new HashSet<DateOnly> { new(2026, 10, 3) };

        var quote = PriceCalculator.Calculate(VincomCard(), VehicleType.Sedan, saturdayHoliday, saturdayHoliday.AddMinutes(120), holidays);
        Assert.Equal(65000, quote.FinalAmount);
        Assert.Equal(1.3m, quote.Multiplier);
    }

    [Fact]
    public void Oversized_vehicle_without_own_rules_uses_suv_price_times_multiplier()
        => Assert.Equal(75000, PriceCalculator.Calculate(VincomCard(), VehicleType.Oversized, Weekday, Weekday.AddMinutes(120)).FinalAmount);

    [Fact]
    public void Daily_cap_is_respected()
        => Assert.Equal(300000, PriceCalculator.Calculate(VincomCard(), VehicleType.Sedan, Weekday, Weekday.AddHours(23)).FinalAmount);

    [Fact]
    public void Calculate_throws_argument_exception_when_end_before_start()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            PriceCalculator.Calculate(VincomCard(), VehicleType.Sedan, Weekday, Weekday.AddMinutes(-10)));
        Assert.Contains("Giờ kết thúc phải sau giờ bắt đầu", ex.Message);
    }

    [Fact]
    public void Multiple_overnight_parking_charges_surcharge_for_each_night()
    {
        // Thứ Sáu 20:00 VN (13:00 UTC) đến Chủ Nhật 08:00 sáng VN (01:00 UTC) -> Đỗ qua 2 đêm liên tiếp
        var entryUtc = new DateTime(2026, 10, 2, 13, 0, 0, DateTimeKind.Utc);
        var exitUtc = new DateTime(2026, 10, 4, 1, 0, 0, DateTimeKind.Utc);

        var quote = PriceCalculator.Calculate(VincomCard(), VehicleType.Sedan, entryUtc, exitUtc);

        var overnightLine = quote.Lines.FirstOrDefault(l => l.Description.Contains("Phụ thu qua đêm"));
        Assert.NotNull(overnightLine);
        Assert.Equal(2, overnightLine.Blocks);            // Số buổi tối = 2
        Assert.Equal(50000m, overnightLine.UnitPrice);    // Đơn giá 50.000đ/đêm
        Assert.Equal(100000m, overnightLine.Amount);      // Tổng phụ thu = 100.000đ
    }
}
