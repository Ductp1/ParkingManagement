using ParkingManagement.SharedKernel.Domain;

namespace GateService.Domain.Entities;

/// <summary>
/// [GateService] Audit log US-066: mỗi lần admin lưu cấu hình sức chứa ghi 1 dòng
/// (ai, lúc nào, C/B/Q cũ → mới, version cũ → mới, warnings trả về cho admin).
/// </summary>
public class LotCapacityAuditLog : BaseEntity
{
    public int ParkingLotId { get; set; }
    public int? ChangedByUserId { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public int OldCapacity { get; set; }
    public int OldTransientBuffer { get; set; }
    public int OldSubscriberQuota { get; set; }
    public int NewCapacity { get; set; }
    public int NewTransientBuffer { get; set; }
    public int NewSubscriberQuota { get; set; }
    public long OldVersion { get; set; }
    public long NewVersion { get; set; }
    public string? WarningsJson { get; set; }
}