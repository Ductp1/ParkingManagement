using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace BookingService.Domain.Entities;

/// <summary>[BookingService] Lịch sử chuyển trạng thái booking – bằng chứng khi tranh chấp.</summary>
public class BookingStatusLog : BaseEntity
{
    public int BookingId { get; set; }
    public BookingStatus? FromStatus { get; set; }
    public BookingStatus ToStatus { get; set; }
    /// <summary>null = hệ thống tự chuyển (worker hết hạn, auto-complete...).</summary>
    public int? ChangedByUserId { get; set; }
    public string? Reason { get; set; }

    public Booking Booking { get; set; } = null!;
}
