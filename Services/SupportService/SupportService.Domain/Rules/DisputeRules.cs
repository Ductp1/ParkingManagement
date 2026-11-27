using ParkingManagement.SharedKernel.Enums;

namespace SupportService.Domain.Rules;

/// <summary>US-090 AC2: phán quyết của chủ bãi.</summary>
public enum DisputeDecision
{
    /// <summary>Ép hoàn tiền cho tài xế (override – RefundReason.DisputeOverride, chủ bãi chịu), ticket → Resolved.</summary>
    ForceRefund = 1,
    /// <summary>Bác khiếu nại, không hoàn tiền, ticket → Rejected.</summary>
    RejectComplaint = 2,
    /// <summary>Chỉ với khiếu nại "chỗ bị chiếm": chuyển tài xế sang chỗ trống khác, giữ nguyên giá, ticket → Resolved.</summary>
    MoveToSlot = 3,
}

/// <summary>Quy tắc chủ bãi phán quyết tranh chấp và SLA xử lý (US-090).</summary>
public static class DisputeRules
{
    /// <summary>AC3: chủ bãi phản hồi đầu tiên trong 24 giờ kể từ khi tài xế gửi khiếu nại.</summary>
    public const int FirstResponseHours = 24;
    /// <summary>AC3: mục tiêu kết luận trong 3 ngày làm việc, hạn chót 5 ngày làm việc.</summary>
    public const int ResolutionTargetBusinessDays = 3;
    public const int ResolutionDeadlineBusinessDays = 5;

    public const int MinReasonLength = 20;
    public const int MaxReasonLength = 4000;
    public const int MinMessageLength = 5;
    public const int MaxMessageLength = 4000;
    public const int MaxAttachments = 10;

    /// <summary>Ngày làm việc tính theo giờ Việt Nam (UTC+7), bỏ thứ Bảy và Chủ nhật.</summary>
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    /// <summary>Kết quả của ticket sau phán quyết.</summary>
    public static ComplaintStatus OutcomeOf(DisputeDecision decision)
        => decision == DisputeDecision.RejectComplaint ? ComplaintStatus.Rejected : ComplaintStatus.Resolved;

    /// <summary>
    /// Chỉ chủ bãi phán quyết, 1 lần duy nhất, khi ticket còn mở và là tranh chấp về bãi
    /// (lỗi ứng dụng / khác không phải tranh chấp với bãi nên không phán quyết ở đây).
    /// </summary>
    public static bool CanOwnerRule(ComplaintStatus status, ComplaintCategory category, bool alreadyRuled)
        => !alreadyRuled && ComplaintRules.RequiresOwnerResponse(category) && ComplaintRules.IsOpen(status);

    /// <summary>Cộng <paramref name="businessDays"/> ngày làm việc (giữ nguyên giờ trong ngày), bỏ qua cuối tuần theo giờ Việt Nam.</summary>
    public static DateTime AddBusinessDays(DateTime startUtc, int businessDays)
    {
        var local = startUtc + VietnamOffset;
        // Bắt đầu vào cuối tuần → tính từ đầu ngày thứ Hai kế tiếp.
        while (local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) local = local.Date.AddDays(1);
        for (var added = 0; added < businessDays;)
        {
            local = local.AddDays(1);
            if (local.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) added++;
        }
        return DateTime.SpecifyKind(local - VietnamOffset, DateTimeKind.Utc);
    }

    /// <summary>Quá hạn: xong sau hạn, hoặc chưa xong mà đã qua hạn.</summary>
    public static bool IsBreached(DateTime dueAtUtc, DateTime? doneAtUtc, DateTime nowUtc)
        => (doneAtUtc ?? nowUtc) > dueAtUtc;
}
