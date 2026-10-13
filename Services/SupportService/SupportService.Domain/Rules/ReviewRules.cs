using ParkingManagement.SharedKernel.Enums;

namespace SupportService.Domain.Rules;

/// <summary>Quy tắc đánh giá & khiếu nại (UC-17, UC-18, Answer_3 §13).</summary>
public static class ReviewRules
{
    /// <summary>Hạn gửi khiếu nại: 7 ngày làm việc (thứ Hai – thứ Sáu) – đã xác nhận với BA (US-088 AC1, AC2).</summary>
    public const int ComplaintWindowWorkingDays = 7;
    public const int OwnerResponseHours = 48;

    /// <summary>Việt Nam dùng UTC+7 quanh năm (không đổi giờ mùa hè).</summary>
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    /// <summary>Chỉ được đánh giá 1–5 sao, và chỉ khi booking đã Completed.</summary>
    public static bool CanReview(BookingStatus bookingStatus, int rating)
        => bookingStatus == BookingStatus.Completed && rating is >= 1 and <= 5;

    /// <summary>Khiếu nại phải gửi trong 7 ngày làm việc kể từ ngày xảy ra sự cố / có quyết định.</summary>
    public static bool IsWithinComplaintWindow(DateTime incidentAtUtc, DateTime nowUtc)
        => nowUtc < ComplaintDeadlineUtc(incidentAtUtc);

    /// <summary>
    /// Hạn chót (UTC, không tính mốc này): hết ngày làm việc thứ 7 sau ngày xảy ra sự cố, theo giờ Việt Nam.
    /// Ngày xảy ra sự cố không tính; bỏ qua thứ Bảy, Chủ nhật. VD sự cố thứ Sáu 09/10 → hạn hết thứ Ba 20/10.
    /// Chưa trừ ngày lễ (Tết, 30/4...) – cần bảng ngày lễ nếu nhóm muốn tính chính xác hơn.
    /// </summary>
    public static DateTime ComplaintDeadlineUtc(DateTime incidentAtUtc)
    {
        var day = (incidentAtUtc + VietnamOffset).Date;
        for (var counted = 0; counted < ComplaintWindowWorkingDays;)
        {
            day = day.AddDays(1);
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) counted++;
        }
        return DateTime.SpecifyKind(day.AddDays(1) - VietnamOffset, DateTimeKind.Utc);
    }

    /// <summary>Chủ bãi quá 48 giờ chưa phản hồi → tự động chuyển Admin phân xử.</summary>
    public static bool ShouldEscalate(ComplaintStatus status, DateTime? ownerResponseDueAtUtc, DateTime nowUtc)
        => status == ComplaintStatus.AwaitingOwner && ownerResponseDueAtUtc is { } due && nowUtc > due;
}
