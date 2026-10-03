using ParkingManagement.SharedKernel.Domain;

namespace BookingService.Domain.Entities;

/// <summary>
/// [BookingService] Price Lock Rule: chụp toàn bộ biểu giá khi booking Confirmed (Đặc tả v3 §4.2).
/// Chủ bãi đổi giá sau đó không ảnh hưởng booking này.
/// </summary>
public class PriceSnapshot : BaseEntity
{
    public int BookingId { get; set; }
    /// <summary>Rate card gốc đã dùng để tính giá.</summary>
    public int RateCardId { get; set; }
    /// <summary>Bản sao rate card + các rule đã áp dụng, dạng JSON.</summary>
    public string RateCardJson { get; set; } = "{}";
    public decimal BaseAmount { get; set; }
    public decimal SurchargeAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public int BillingGracePeriodMinutes { get; set; } = 15;
    public decimal OverstayMultiplier { get; set; } = 1.5m;

    public Booking Booking { get; set; } = null!;
}
