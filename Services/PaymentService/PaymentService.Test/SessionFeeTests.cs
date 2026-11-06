using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Services;
using Xunit;

namespace PaymentService.Test;

/// <summary>
/// UC-22 / US-045 → US-047: chốt phí lượt gửi xe thực tế tại cổng ra.
/// US-046: Billing Grace 15' miễn phí · US-047: quá giờ kết thúc + 15' ân hạn tính từ giờ kết thúc × hệ số 1.5–2.0.
/// </summary>
public class SessionFeeTests
{
    // Thứ Sáu 02/10/2026 02:00 UTC = 09:00 giờ VN (ngày thường, không nhân hệ số).
    private static readonly DateTime Entry = new(2026, 10, 2, 2, 0, 0, DateTimeKind.Utc);

    private static RateCard NewCard(decimal overstayMultiplier = 1.5m)
    {
        var card = new RateCard { OverstayMultiplier = overstayMultiplier };
        card.Rules.Add(new RateRule { VehicleType = VehicleType.Sedan, FromMinute = 0, ToMinute = 60, BlockMinutes = 60, PricePerBlock = 10000 });
        card.Rules.Add(new RateRule { VehicleType = VehicleType.Sedan, FromMinute = 60, ToMinute = null, BlockMinutes = 60, PricePerBlock = 20000 });
        return card;
    }

    [Fact] // US-046 AC1: vào rồi ra trong 14 phút → 0đ
    public void Session_within_billing_grace_is_free()
    {
        var q = PriceCalculator.CalculateSession(NewCard(), VehicleType.Sedan, Entry, Entry.AddMinutes(14));
        Assert.True(q.IsWithinBillingGrace);
        Assert.Equal(0, q.FinalAmount);
        Assert.Equal(0, q.OverstayMinutes);
    }

    [Fact] // US-046 AC2: vượt 15 phút → tính theo biểu giá (không có booking)
    public void Session_beyond_billing_grace_billed_by_rate_card()
    {
        var q = PriceCalculator.CalculateSession(NewCard(), VehicleType.Sedan, Entry, Entry.AddMinutes(61));
        Assert.False(q.IsWithinBillingGrace);
        Assert.Equal(30000, q.BaseAmount);   // 60' đầu 10.000 + block lẻ 1' làm tròn lên 1 block 20.000
        Assert.Equal(30000, q.FinalAmount);
        Assert.Equal(0, q.OverstayMinutes);  // khách vãng lai không có giờ kết thúc đặt
    }

    [Fact] // US-047 AC1: ra sau giờ kết thúc + 15' ân hạn → cước quá giờ tính từ giờ kết thúc × 1.5
    public void Overstay_after_end_plus_grace_is_multiplied()
    {
        // Đặt 1 giờ (kết thúc 03:00 UTC), ra lúc 03:35 → quá giờ 35' (tính từ giờ kết thúc).
        var bookedEnd = Entry.AddMinutes(60);
        var q = PriceCalculator.CalculateSession(NewCard(overstayMultiplier: 1.5m), VehicleType.Sedan,
            Entry, Entry.AddMinutes(95), bookedEndUtc: bookedEnd);

        Assert.Equal(95, q.DurationMinutes);
        Assert.Equal(35, q.OverstayMinutes);
        // Quá giờ 35' theo đơn giá chuẩn: rơi vào bậc đầu (10.000đ/block 60') → 10.000 × 1.5 = 15.000
        Assert.Equal(15000, q.OverstayAmount);
        // Cước gốc 95': 60' đầu 10.000 + 35' sau = 1 block 20.000 → 30.000; tổng 30.000 + 15.000 quá giờ
        Assert.Equal(45000, q.FinalAmount);
    }

    [Fact] // Ra trong vòng kết thúc + 15' ân hạn → không tính quá giờ
    public void Exit_within_overstay_grace_has_no_penalty()
    {
        var bookedEnd = Entry.AddMinutes(60);
        var q = PriceCalculator.CalculateSession(NewCard(), VehicleType.Sedan,
            Entry, Entry.AddMinutes(70), bookedEndUtc: bookedEnd);

        Assert.Equal(0, q.OverstayMinutes);
        Assert.Equal(0, q.OverstayAmount);
        Assert.Equal(30000, q.FinalAmount);   // 70' thực đỗ: 10.000 + 20.000
    }

    [Fact] // Ra sớm hơn giờ đã đặt → chỉ tính thời gian thực đỗ
    public void Early_exit_billed_on_actual_duration_only()
    {
        var bookedEnd = Entry.AddMinutes(180);
        var q = PriceCalculator.CalculateSession(NewCard(), VehicleType.Sedan,
            Entry, Entry.AddMinutes(30), bookedEndUtc: bookedEnd);

        Assert.Equal(0, q.OverstayMinutes);
        Assert.Equal(10000, q.FinalAmount);   // 30' → block đầu 10.000
    }

    [Fact] // Hệ số quá giờ cấu hình theo bãi (2.0) được ưu tiên hơn RateCard mặc định
    public void Snapshot_overstay_multiplier_overrides_rate_card()
    {
        var bookedEnd = Entry.AddMinutes(60);
        var q = PriceCalculator.CalculateSession(NewCard(overstayMultiplier: 1.5m), VehicleType.Sedan,
            Entry, Entry.AddMinutes(95), bookedEndUtc: bookedEnd, overstayMultiplier: 2.0m);

        // Quá giờ 35' → 10.000 (đơn giá chuẩn) × 2.0 = 20.000 (hệ số 2.0 từ snapshot thắng hệ số 1.5 của card)
        Assert.Equal(20000, q.OverstayAmount);
        Assert.Equal(50000, q.FinalAmount);
    }

    [Fact]
    public void Exit_before_entry_is_rejected()
        => Assert.Throws<ArgumentException>(() => PriceCalculator.CalculateSession(
            NewCard(), VehicleType.Sedan, Entry.AddMinutes(10), Entry));
}
