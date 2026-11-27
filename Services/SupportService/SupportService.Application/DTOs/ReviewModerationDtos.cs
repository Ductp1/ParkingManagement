namespace SupportService.Application.DTOs;

/// <summary>Body của POST /api/v1/reviews/{id}/owner-reply – phản hồi công khai của chủ bãi (US-094 AC1).</summary>
public sealed record OwnerReplyRequestDto(string Reply);

/// <summary>Body của POST /api/v1/reviews/{id}/report (chủ bãi) và /hide, /dismiss-report (Admin) – lý do bắt buộc.</summary>
public sealed record ReviewReasonRequestDto(string Reason);

/// <summary>Đánh giá nhìn từ phía chủ bãi / Admin, kèm trạng thái phản hồi và khiếu nại.</summary>
public sealed record ModeratedReviewDto(
    int Id,
    int ParkingLotId,
    string ReviewerName,
    int Rating,
    string? Comment,
    string? OwnerReply,
    DateTime? OwnerRepliedAtUtc,
    bool IsReported,
    string? ReportReason,
    DateTime? ReportedAtUtc,
    bool IsHidden,
    DateTime CreatedAtUtc);
