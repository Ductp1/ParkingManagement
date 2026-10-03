using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace SupportService.Domain.Entities;

/// <summary>
/// [SupportService] Khiếu nại / tranh chấp 3 bên (UC-17, UC-38, UC-42, FLOW 9). Module: TV9.
/// Gửi trong 7 ngày; chủ bãi phản hồi trong 48 giờ; quá hạn thì escalate lên Admin.
/// Khoản hoàn tiền (nếu có) nằm ở bảng Refunds với ComplaintId trỏ về đây.
/// </summary>
public class Complaint : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public int UserId { get; set; }
    /// <summary>→ BookingService (không FK).</summary>
    public int? BookingId { get; set; }
    public string? BookingCode { get; set; }
    /// <summary>→ ParkingService (không FK).</summary>
    public int ParkingLotId { get; set; }
    /// <summary>→ UserService (không FK).</summary>
    public int OwnerProfileId { get; set; }

    public ComplaintCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? EvidenceUrlsJson { get; set; }
    public decimal? RequestedAmount { get; set; }
    public ComplaintStatus Status { get; set; } = ComplaintStatus.Open;

    public string? OwnerResponse { get; set; }
    public DateTime? OwnerResponseDueAtUtc { get; set; }
    public DateTime? OwnerRespondedAtUtc { get; set; }

    public DateTime? EscalatedAtUtc { get; set; }
    public string? Resolution { get; set; }
    public decimal? ResolvedRefundAmount { get; set; }
    public int? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    /// <summary>Đang giữ tiền quyết toán của chủ bãi cho tới khi xử lý xong.</summary>
    public bool PayoutHeld { get; set; }

    public ICollection<ComplaintMessage> Messages { get; set; } = new List<ComplaintMessage>();

}
