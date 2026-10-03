using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace BookingService.Domain.Entities;

/// <summary>
/// [BookingService] Lịch sử sửa / gia hạn booking (US-029, US-030, UC-14, UC-15).
/// Mỗi lần đổi giờ, đổi xe hay gia hạn ghi 1 dòng: giá trị cũ, giá trị mới và tiền chênh lệch phải trả/hoàn.
/// </summary>
public class BookingModification : BaseEntity
{
    public int BookingId { get; set; }
    public BookingModificationType ModificationType { get; set; }
    public DateTime? OldStartAtUtc { get; set; }
    public DateTime? OldEndAtUtc { get; set; }
    public DateTime? NewStartAtUtc { get; set; }
    public DateTime? NewEndAtUtc { get; set; }
    public int? OldVehicleId { get; set; }
    public int? NewVehicleId { get; set; }
    public int? OldSlotId { get; set; }
    public int? NewSlotId { get; set; }
    /// <summary>Dương = khách trả thêm, âm = hoàn lại cho khách.</summary>
    public decimal PriceDifference { get; set; }
    /// <summary>Payment bù chênh lệch bên PaymentService (không FK).</summary>
    public int? PaymentId { get; set; }
    public int RequestedByUserId { get; set; }

    public Booking Booking { get; set; } = null!;
}
