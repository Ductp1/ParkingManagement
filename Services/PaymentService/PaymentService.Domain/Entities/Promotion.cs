using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>[PaymentService] Mã khuyến mãi (UC-31). Bên tài trợ quyết định trừ vào doanh thu của ai.</summary>
public class Promotion : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>null = áp dụng toàn sàn.</summary>
    public int? ParkingLotId { get; set; }
    /// <summary>Chủ bãi tài trợ; null = nền tảng tài trợ.</summary>
    public int? OwnerProfileId { get; set; }
    public PromotionSponsor Sponsor { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<PromotionRedemption> Redemptions { get; set; } = new List<PromotionRedemption>();

}
