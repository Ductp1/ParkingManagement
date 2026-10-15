using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PaymentService.API.Controllers;
using PaymentService.Infrastructure.Persistence;

namespace ParkingManagement.IntegrationTests.PerService;

/// <summary>Collection: PaymentService chạy host thật trên database pm_it_payment_* riêng của từng lần chạy.</summary>
[CollectionDefinition("Payment API")]
public sealed class PaymentApiCollection : ICollectionFixture<ServiceHostFixture<PricingController, PaymentDbContext>>;

/// <summary>
/// Integration test PaymentService (TV4): công cụ tính giá UC-11 (US-070/071 – ân hạn 15 phút, bậc giá lũy tiến,
/// hệ số cuối tuần) và tra cứu giao dịch theo booking (UC-19) trên dữ liệu seed khớp BookingService.
/// Ngày dùng để tính giá cố tình chọn ở năm kế tiếp, rơi vào ngày thường → hệ số nhân = 1, không dính lễ/weekend seed 2026.
/// </summary>
[Collection("Payment API")]
public class PaymentServiceApiTests(ServiceHostFixture<PricingController, PaymentDbContext> fixture)
{
    /// <summary>Khung giờ 2 tiếng bắt đầu 09:00 giờ VN của một ngày thường (Thứ 2–6) trong năm kế tiếp.</summary>
    private static (DateTime Start, DateTime End) WeekdayWindow(int hours = 2)
    {
        var vnDate = new DateOnly(DateTime.UtcNow.Year + 1, 3, 10);
        while (vnDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) vnDate = vnDate.AddDays(1);
        var start = new DateTime(vnDate.Year, vnDate.Month, vnDate.Day, 2, 0, 0, DateTimeKind.Utc); // 09:00 VN
        return (start, start.AddHours(hours));
    }

    [Fact] // US-070: 2 tiếng trong bậc đầu (60′ × 30.000đ) + bậc hai (60′ × 20.000đ) = 50.000đ – khớp PM-0001 của BK-0001.
    public async Task Quote_for_two_hours_uses_progressive_tiers()
    {
        var (start, end) = WeekdayWindow();
        var quote = await fixture.Client.GetFromJsonAsync<QuoteJson>(
            $"/api/v1/pricing/quote?parkingLotId=1&vehicleType=Sedan&startAtUtc={start:O}&endAtUtc={end:O}");

        Assert.Equal(1, quote!.ParkingLotId);
        Assert.Equal(1, quote.RateCardId);
        Assert.Equal("Sedan", quote.VehicleType);
        Assert.Equal(120, quote.DurationMinutes);
        Assert.Equal(50000m, quote.BaseAmount);
        Assert.Equal(1m, quote.Multiplier);
        Assert.Equal(50000m, quote.FinalAmount);
        Assert.False(quote.IsWithinGracePeriod);

        // Chi tiết bậc giá minh bạch: bậc 1 đủ 60′, bậc 2 đủ 60′
        Assert.Equal(2, quote.Lines.Count);
        Assert.Equal(30000m, quote.Lines[0].UnitPrice);
        Assert.Equal(30000m, quote.Lines[0].Amount);
        Assert.Equal(20000m, quote.Lines[1].UnitPrice);
        Assert.Equal(20000m, quote.Lines[1].Amount);
    }

    [Fact] // 3 tiếng = 70.000đ – khớp đúng TotalAmount seed của BK-0002 (Booking ↔ Payment đồng nhất bảng giá).
    public async Task Quote_for_three_hours_matches_seed_amount_of_bk_0002()
    {
        var (start, end) = WeekdayWindow(hours: 3);
        var quote = await fixture.Client.GetFromJsonAsync<QuoteJson>(
            $"/api/v1/pricing/quote?parkingLotId=1&vehicleType=Sedan&startAtUtc={start:O}&endAtUtc={end:O}");

        Assert.Equal(180, quote!.DurationMinutes);
        Assert.Equal(70000m, quote.BaseAmount);
        Assert.Equal(70000m, quote.FinalAmount);
        Assert.Equal(3, quote.Lines.Count);
    }

    [Fact] // US-071: đỗ ≤ 15 phút thuộc ân hạn → miễn phí hoàn toàn, không sinh dòng giá.
    public async Task Quote_within_the_grace_period_is_free()
    {
        var (start, end) = WeekdayWindow();
        var quote = await fixture.Client.GetFromJsonAsync<QuoteJson>(
            $"/api/v1/pricing/quote?parkingLotId=1&vehicleType=Sedan&startAtUtc={start:O}&endAtUtc={start.AddMinutes(10):O}");

        Assert.True(quote!.IsWithinGracePeriod);
        Assert.Equal(10, quote.DurationMinutes);
        Assert.Equal(0m, quote.BaseAmount);
        Assert.Equal(0m, quote.FinalAmount);
        Assert.Empty(quote.Lines);
    }

    [Theory] // Validate đầu vào của UC-11 → 400 ProblemDetails với đúng thông điệp nghiệp vụ.
    [InlineData(-1, "parkingLotId phải là số nguyên dương")]
    public async Task Quote_rejects_invalid_input(int parkingLotId, string expectedFragment)
    {
        var (start, end) = WeekdayWindow();
        await Asserts.BadRequestAsync(await fixture.Client.GetAsync(
                $"/api/v1/pricing/quote?parkingLotId={parkingLotId}&vehicleType=Sedan&startAtUtc={start:O}&endAtUtc={end:O}"),
            expectedFragment);
    }

    [Fact]
    public async Task Quote_rejects_reversed_window_and_over_30_days()
    {
        var (start, end) = WeekdayWindow();
        await Asserts.BadRequestAsync(await fixture.Client.GetAsync(
                $"/api/v1/pricing/quote?parkingLotId=1&vehicleType=Sedan&startAtUtc={start:O}&endAtUtc={start.AddMinutes(-5):O}"),
            "Giờ kết thúc phải sau giờ bắt đầu");

        await Asserts.BadRequestAsync(await fixture.Client.GetAsync(
                $"/api/v1/pricing/quote?parkingLotId=1&vehicleType=Sedan&startAtUtc={start:O}&endAtUtc={start.AddDays(31):O}"),
            "Thời gian đỗ tối đa 30 ngày");
    }

    [Fact] // Bãi không có bảng giá hiệu lực → 404 "Bảng giá của bãi".
    public async Task Quote_for_lot_without_rate_card_returns_404()
    {
        var (start, end) = WeekdayWindow();
        await Asserts.NotFoundAsync(await fixture.Client.GetAsync(
            $"/api/v1/pricing/quote?parkingLotId=99&vehicleType=Sedan&startAtUtc={start:O}&endAtUtc={end:O}"));
    }

    [Fact] // UC-19: lịch sử giao dịch của booking – PM-0001 đã thành công 50.000đ qua VNPAY cho BK-0001.
    public async Task Payments_by_booking_return_the_seed_transactions()
    {
        var forBk1 = await fixture.Client.GetFromJsonAsync<List<PaymentJson>>("/api/v1/payments?bookingId=1");
        var paid = Assert.Single(forBk1!);
        Assert.Equal("PM-0001", paid.Code);
        Assert.Equal("BK-0001", paid.BookingCode);
        Assert.Equal("Vnpay", paid.Method);
        Assert.Equal("Succeeded", paid.Status);
        Assert.Equal(50000m, paid.Amount);

        var forBk2 = await fixture.Client.GetFromJsonAsync<List<PaymentJson>>("/api/v1/payments?bookingId=2");
        var confirmed = Assert.Single(forBk2!);
        Assert.Equal("BK-0002", confirmed.BookingCode);
        Assert.Equal(63000m, confirmed.Amount);

        // Booking không tồn tại → danh sách rỗng (không lỗi)
        var none = await fixture.Client.GetFromJsonAsync<List<PaymentJson>>("/api/v1/payments?bookingId=999");
        Assert.Empty(none!);

        await Asserts.BadRequestAsync(await fixture.Client.GetAsync("/api/v1/payments?bookingId=0"),
            "bookingId phải là số nguyên dương");
    }

    public sealed record QuoteJson(int ParkingLotId, int RateCardId, string VehicleType, int DurationMinutes,
        decimal BaseAmount, decimal Multiplier, decimal FinalAmount, bool IsWithinGracePeriod, IReadOnlyList<PriceLineJson> Lines);

    public sealed record PriceLineJson(string Description, int Minutes, int Blocks, decimal UnitPrice, decimal Amount);

    public sealed record PaymentJson(int Id, string Code, int? BookingId, string? BookingCode, string Method, string Status,
        decimal Amount, decimal RefundedAmount, string? ProviderTransactionId, DateTime? PaidAtUtc, string? InvoiceNumber);
}
