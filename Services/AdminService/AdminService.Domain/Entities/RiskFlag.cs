using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace AdminService.Domain.Entities;

/// <summary>
/// [AdminService] Cảnh báo gian lận / bất thường cần Admin xem xét (US-077, US-103, US-104, Kiến trúc v3 §3.2 Anomaly Detection):
///  - STAFF_ABNORMAL_CANCEL: Staff hủy no-show nhiều bất thường trong ca.
///  - BOOKING_ABUSE: tài xế đặt/hủy liên tục để giữ chỗ ảo.
///  - LEAKAGE: số xe qua barie cao bất thường so với số booking (né nền tảng).
/// Alert tiers: Info xử lý trong 24h, Warning trong 4h, Critical trong 15 phút.
/// </summary>
public class RiskFlag : BaseEntity
{
    public RiskSubjectType SubjectType { get; set; }
    /// <summary>Id của User / ParkingLot / OwnerProfile tương ứng (không FK).</summary>
    public int SubjectId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public RiskSeverity Severity { get; set; }
    public RiskFlagStatus Status { get; set; } = RiskFlagStatus.Open;
    public string Description { get; set; } = string.Empty;
    public string? EvidenceJson { get; set; }
    public DateTime DueAtUtc { get; set; }
    public int? AssignedToUserId { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNote { get; set; }
    /// <summary>Chế tài được áp dụng sau khi xác minh (nếu có).</summary>
    public int? SanctionId { get; set; }

    public Sanction? Sanction { get; set; }
}
