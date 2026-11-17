using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Domain.Entities;

/// <summary>
/// [NotificationService] Job nhắc lịch booking (US-084): 2 job/booking — trước giờ đến và trước giờ hết hạn.
/// Booking hủy/đổi giờ → job Pending bị Cancelled (hoặc reschedule = cancel + tạo mới).
/// Worker quét DueAtUtc <= now theo batch; gửi xong đánh dấu Sent. Bảng nhỏ, poll 15s là đủ.
/// </summary>
public class ReminderJob : BaseEntity
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    /// <summary>→ UserService (không FK).</summary>
    public int UserId { get; set; }
    public int ParkingLotId { get; set; }
    public string LotName { get; set; } = string.Empty;
    public string? SlotCode { get; set; }
    public ReminderKind Kind { get; set; }
    /// <summary>Mốc thời gian mà job nhắc tới (giờ đến hoặc giờ hết hạn của booking) — dùng render placeholder.</summary>
    public DateTime TargetTimeUtc { get; set; }
    public DateTime DueAtUtc { get; set; }
    public ReminderJobStatus Status { get; set; } = ReminderJobStatus.Pending;
    public DateTime? SentAtUtc { get; set; }
}