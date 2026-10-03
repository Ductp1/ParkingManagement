using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace AdminService.Domain.Entities;

/// <summary>[AdminService] Chế tài SLA 4 cấp áp lên chủ bãi / bãi (UC-41, Nghiệp vụ v3 §4.1).</summary>
public class Sanction : BaseEntity
{
    /// <summary>→ UserService (không FK).</summary>
    public int OwnerProfileId { get; set; }
    /// <summary>null = áp lên toàn bộ chủ bãi.</summary>
    public int? ParkingLotId { get; set; }
    public SanctionLevel Level { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceJson { get; set; }
    public DateTime StartsAtUtc { get; set; }
    /// <summary>null = vĩnh viễn (cấp 4).</summary>
    public DateTime? EndsAtUtc { get; set; }
    public SanctionStatus Status { get; set; } = SanctionStatus.Active;
    /// <summary>Tiền phạt trừ vào quyết toán / ký quỹ của chủ bãi (nếu có) – dùng để đền bù khách (Answer_2 §10).</summary>
    public decimal? PenaltyAmount { get; set; }
    public int IssuedByUserId { get; set; }

}
