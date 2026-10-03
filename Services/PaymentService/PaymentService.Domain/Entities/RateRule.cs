using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>
/// [PaymentService] 1 bậc giá lũy tiến. VD xe Sedan: phút 0–60 giá 30.000đ/block 60′,
/// phút 60–240 giá 20.000đ/block 60′, từ phút 240 trở đi giá 15.000đ/block 60′.
/// </summary>
public class RateRule : BaseEntity
{
    public int RateCardId { get; set; }
    public VehicleType VehicleType { get; set; }
    public int FromMinute { get; set; }
    /// <summary>null = không giới hạn trên.</summary>
    public int? ToMinute { get; set; }
    public int BlockMinutes { get; set; } = 60;
    public decimal PricePerBlock { get; set; }
    public int SortOrder { get; set; }

    public RateCard RateCard { get; set; } = null!;
}
