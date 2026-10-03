using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Domain.Entities;

/// <summary>[GateService] Ca trực của nhân viên cổng – chốt ca, bàn giao, cảnh báo bất thường (US-076, US-077).</summary>
public class Shift : BaseEntity
{
    /// <summary>→ ParkingService (không FK).</summary>
    public int ParkingLotId { get; set; }
    /// <summary>→ UserService (không FK).</summary>
    public int OwnerProfileId { get; set; }
    public int StaffUserId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;
    public int CheckInCount { get; set; }
    public int CheckOutCount { get; set; }
    public int NoShowCancelCount { get; set; }
    public int ManualExceptionCount { get; set; }
    public decimal CashCollected { get; set; }
    public string? HandoverNote { get; set; }

    public ICollection<GateEvent> Events { get; set; } = new List<GateEvent>();
}
