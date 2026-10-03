using ParkingManagement.SharedKernel.Enums;

namespace SupportService.Domain.Rules;

/// <summary>Quy tắc đánh giá & khiếu nại (UC-17, UC-18, Answer_3 §13).</summary>
public static class ReviewRules
{
    public const int ComplaintWindowDays = 7;
    public const int OwnerResponseHours = 48;

    /// <summary>Chỉ được đánh giá 1–5 sao, và chỉ khi booking đã Completed.</summary>
    public static bool CanReview(BookingStatus bookingStatus, int rating)
        => bookingStatus == BookingStatus.Completed && rating is >= 1 and <= 5;

    /// <summary>Khiếu nại phải gửi trong 7 ngày kể từ khi booking kết thúc.</summary>
    public static bool IsWithinComplaintWindow(DateTime bookingEndedAtUtc, DateTime nowUtc)
        => nowUtc - bookingEndedAtUtc <= TimeSpan.FromDays(ComplaintWindowDays);

    /// <summary>Chủ bãi quá 48 giờ chưa phản hồi → tự động chuyển Admin phân xử.</summary>
    public static bool ShouldEscalate(ComplaintStatus status, DateTime? ownerResponseDueAtUtc, DateTime nowUtc)
        => status == ComplaintStatus.AwaitingOwner && ownerResponseDueAtUtc is { } due && nowUtc > due;
}
