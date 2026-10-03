using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace ParkingService.Domain.Entities;

/// <summary>
/// [ParkingService] Lịch sử đổi trạng thái slot (US-049, US-065, FR-PARK-012): ai/nguồn nào đổi, từ trạng thái nào sang trạng thái nào.
/// Là bằng chứng khi tranh chấp "bãi báo trống nhưng thực tế có xe" và để phát hiện cảm biến sai lệch.
/// </summary>
public class SlotStateLog : BaseEntity
{
    public int SlotId { get; set; }
    public SlotState FromState { get; set; }
    public SlotState ToState { get; set; }
    public SlotStateSource Source { get; set; }
    /// <summary>Booking / lượt gửi xe gây ra thay đổi (BookingService / GateService, không FK).</summary>
    public int? BookingId { get; set; }
    public int? ParkingSessionId { get; set; }
    /// <summary>Staff ghi đè thủ công (UserService, không FK).</summary>
    public int? ChangedByUserId { get; set; }
    public string? Reason { get; set; }

    public Slot Slot { get; set; } = null!;
}
