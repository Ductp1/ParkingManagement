using ParkingManagement.SharedKernel.Domain;

namespace PaymentService.Domain.Entities;

/// <summary>
/// [PaymentService] Mỗi lần mã khuyến mãi được dùng (US-073). Dùng để kiểm tra giới hạn lượt dùng
/// và phân bổ chi phí: khuyến mãi của chủ bãi trừ vào doanh thu chủ bãi, của nền tảng trừ vào nền tảng.
/// </summary>
public class PromotionRedemption : BaseEntity
{
    public int PromotionId { get; set; }
    /// <summary>→ UserService / BookingService (không FK).</summary>
    public int UserId { get; set; }
    public int BookingId { get; set; }
    public int? PaymentId { get; set; }
    public decimal DiscountAmount { get; set; }
    /// <summary>Hủy booking được hoàn tiền thì trả lại lượt dùng mã.</summary>
    public bool IsReverted { get; set; }

    public Promotion Promotion { get; set; } = null!;
    public Payment? Payment { get; set; }
}
