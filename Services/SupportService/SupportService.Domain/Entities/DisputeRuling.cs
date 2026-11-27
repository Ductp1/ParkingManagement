using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using SupportService.Domain.Rules;

namespace SupportService.Domain.Entities;

/// <summary>
/// [SupportService] Phán quyết của chủ bãi cho 1 tranh chấp (US-090). Module: TV9.
/// Mỗi tranh chấp đúng 1 phán quyết, chỉ chủ bãi của bãi bị khiếu nại được ra.
/// <b>Chỉ thêm, không sửa / xóa</b> (AC4): DB có trigger chặn UPDATE/DELETE. Các phán quyết đồng thời là <b>tiền lệ</b> (AC2).
/// </summary>
public class DisputeRuling : BaseEntity
{
    public int ComplaintId { get; set; }
    public DisputeDecision Decision { get; set; }
    /// <summary>Chỉ có với <see cref="DisputeDecision.ForceRefund"/>.</summary>
    public decimal? RefundAmount { get; set; }
    /// <summary>Chỉ có với <see cref="DisputeDecision.MoveToSlot"/>: chỗ trống mới của tài xế.</summary>
    public string? NewSlotCode { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>→ UserService (không FK). Tài khoản chủ bãi ra phán quyết.</summary>
    public int DecidedByUserId { get; set; }

    /// <summary>Chép từ ticket để tra cứu tiền lệ không cần join.</summary>
    public ComplaintCategory Category { get; set; }
    public int ParkingLotId { get; set; }

    public Complaint Complaint { get; set; } = null!;

    public ComplaintStatus Outcome => DisputeRules.OutcomeOf(Decision);
}
