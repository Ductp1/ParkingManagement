using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using SupportService.Domain.Rules;

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
    /// <summary>→ UserService (không FK). Null khi chưa xác định được chủ bãi → ticket do nền tảng xử lý.</summary>
    public int? OwnerProfileId { get; set; }

    public ComplaintCategory Category { get; set; }
    /// <summary>Mức ưu tiên phân loại lúc tạo ticket (US-088 AC3).</summary>
    public ComplaintPriority Priority { get; set; } = ComplaintPriority.Low;
    /// <summary>Nhóm đang xử lý: Support hoặc Supervisor.</summary>
    public SupportTeam AssignedTeam { get; set; } = SupportTeam.Support;
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
    /// <summary>
    /// Đang giữ tiền quyết toán của chủ bãi cho tới khi có phán quyết (US-089 AC3).
    /// Chỉ giữ khi biết chủ bãi; tự hết giữ khi ticket đóng (<see cref="Close"/>). DB có CHECK constraint bảo đảm 2 điều này.
    /// </summary>
    public bool PayoutHeld { get; set; }

    /// <summary>
    /// Optimistic concurrency: worker escalate và chủ bãi phản hồi có thể cập nhật cùng ticket cùng lúc –
    /// bên lưu sau bị từ chối thay vì ghi đè trạng thái của bên kia. PostgreSQL: ServiceDbContext tự đóng dấu ở SaveChanges.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];

    // ===== US-090: chủ bãi phán quyết tranh chấp (duy nhất 1 lần, không ai khác can thiệp), SLA =====

    /// <summary>AC3: lần đầu chủ bãi phản hồi tài xế (tin nhắn / phán quyết) – tính cùng phản hồi US-089.</summary>
    public DateTime? FirstResponseAtUtc { get; set; }

    public ICollection<ComplaintMessage> Messages { get; set; } = new List<ComplaintMessage>();
    /// <summary>Phán quyết của chủ bãi (null = chưa phán quyết). Không sửa / xóa được.</summary>
    public DisputeRuling? Ruling { get; set; }
    /// <summary>Biên bản phạt xe đậu sai chỗ lập khi chủ bãi phán quyết khiếu nại "chỗ bị chiếm".</summary>
    public ICollection<ParkingViolation> Violations { get; set; } = new List<ParkingViolation>();

    /// <summary>AC3: phản hồi đầu tiên của chủ bãi – tin nhắn ở US-090 hoặc phản hồi kèm bằng chứng ở US-089.</summary>
    public DateTime? FirstRespondedAtUtc => FirstResponseAtUtc ?? OwnerRespondedAtUtc;

    /// <summary>AC3: ghi nhận phản hồi đầu tiên (các lần sau không đổi mốc).</summary>
    public void RecordFirstResponse(DateTime nowUtc) => FirstResponseAtUtc ??= nowUtc;

    /// <summary>
    /// AC2: chủ bãi phán quyết – đóng ticket qua <see cref="Close"/> (giải phóng khoản giữ) và gắn 1 phán quyết không sửa được.
    /// Mỗi tranh chấp chỉ 1 phán quyết; dữ liệu phải khớp loại phán quyết.
    /// </summary>
    public DisputeRuling ApplyOwnerRuling(int ownerUserId, DisputeDecision decision, decimal? refundAmount, string? newSlotCode,
        string reason, DateTime nowUtc)
    {
        if (!DisputeRules.CanOwnerRule(Status, Category, Ruling is not null))
            throw new InvalidOperationException($"Tranh chấp {Code} không còn chờ chủ bãi phán quyết.");
        var valid = decision switch
        {
            DisputeDecision.ForceRefund => refundAmount > 0 && newSlotCode is null,
            DisputeDecision.RejectComplaint => refundAmount is null && newSlotCode is null,
            DisputeDecision.MoveToSlot => Category == ComplaintCategory.SlotOccupied && !string.IsNullOrWhiteSpace(newSlotCode) && refundAmount is null,
            _ => false,
        };
        if (!valid) throw new ArgumentException($"Dữ liệu không khớp với phán quyết {decision}.");

        Close(DisputeRules.OutcomeOf(decision), reason, refundAmount, ownerUserId, nowUtc);
        OwnerRespondedAtUtc ??= nowUtc;
        RecordFirstResponse(nowUtc);
        Ruling = new DisputeRuling
        {
            Decision = decision,
            RefundAmount = refundAmount,
            NewSlotCode = newSlotCode,
            Reason = reason,
            DecidedByUserId = ownerUserId,
            Category = Category,
            ParkingLotId = ParkingLotId,
        };
        return Ruling;
    }

    /// <summary>
    /// Đóng tranh chấp khi có phán quyết (US-090) và <b>giải phóng khoản tiền đang giữ</b> (US-089 AC3).
    /// Mọi luồng kết thúc tranh chấp phải đi qua đây để không còn ticket đã đóng mà vẫn giữ tiền.
    /// </summary>
    public void Close(ComplaintStatus outcome, string resolution, decimal? refundAmount, int? resolvedByUserId, DateTime nowUtc)
    {
        if (outcome is not (ComplaintStatus.Resolved or ComplaintStatus.Rejected))
            throw new ArgumentException("Tranh chấp chỉ đóng với trạng thái Resolved hoặc Rejected.", nameof(outcome));
        if (Status is ComplaintStatus.Resolved or ComplaintStatus.Rejected)
            throw new InvalidOperationException($"Tranh chấp {Code} đã được đóng trước đó.");
        if (refundAmount < 0) throw new ArgumentOutOfRangeException(nameof(refundAmount), "Số tiền hoàn không được âm.");

        Status = outcome;
        Resolution = resolution;
        ResolvedRefundAmount = refundAmount;
        ResolvedByUserId = resolvedByUserId;
        ResolvedAtUtc = nowUtc;
        PayoutHeld = false;
    }
}
