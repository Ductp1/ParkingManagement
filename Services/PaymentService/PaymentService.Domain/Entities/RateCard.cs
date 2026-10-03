using ParkingManagement.SharedKernel.Domain;

namespace PaymentService.Domain.Entities;

/// <summary>
/// [PaymentService] Biểu giá của 1 bãi (UC-30). Module: TV4.
/// Giá lũy tiến theo block giờ nằm ở RateRule; các hệ số phụ thu nằm ở đây.
/// </summary>
public class RateCard : BaseEntity
{
    /// <summary>→ ParkingService (không FK).</summary>
    public int ParkingLotId { get; set; }
    /// <summary>→ UserService (không FK).</summary>
    public int OwnerProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Phụ thu qua đêm (VND/lượt) nếu phiên đỗ đi qua khung 22:00–06:00.</summary>
    public decimal OvernightSurcharge { get; set; }
    public decimal WeekendMultiplier { get; set; } = 1.0m;
    public decimal HolidayMultiplier { get; set; } = 1.0m;
    /// <summary>Xe quá khổ: 1.5x – 2.0x (Đặc tả v3 §2.5).</summary>
    public decimal OversizedMultiplier { get; set; } = 1.5m;
    /// <summary>Quá giờ đặt: 1.5x – 2.0x đơn giá chuẩn (Đặc tả v3 §4.4).</summary>
    public decimal OverstayMultiplier { get; set; } = 1.5m;
    /// <summary>Trần giá/ngày; null = không giới hạn.</summary>
    public decimal? MaxDailyAmount { get; set; }

    public ICollection<RateRule> Rules { get; set; } = new List<RateRule>();
}
