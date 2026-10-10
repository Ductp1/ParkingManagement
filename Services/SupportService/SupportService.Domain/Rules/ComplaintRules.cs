using ParkingManagement.SharedKernel.Enums;

namespace SupportService.Domain.Rules;

/// <summary>Mức ưu tiên của ticket (US-088 AC3).</summary>
public enum ComplaintPriority { Low = 1, Medium = 2, High = 3 }

/// <summary>Nhóm xử lý ticket: Support (CSKH) hoặc Supervisor (giám sát).</summary>
public enum SupportTeam { Support = 1, Supervisor = 2 }

/// <summary>Quy tắc tạo và phân loại khiếu nại / ticket hỗ trợ (US-088).</summary>
public static class ComplaintRules
{
    public const int MaxEvidenceFiles = 5;
    public const int MinDescriptionLength = 10;
    public const int MaxDescriptionLength = 4000;

    /// <summary>
    /// AC3: Low → Support; Medium (lỗi booking/thanh toán) → Support; High (mất tiền, không được phục vụ) → Supervisor.
    /// </summary>
    public static (ComplaintPriority Priority, SupportTeam Team) Classify(ComplaintCategory category) => category switch
    {
        ComplaintCategory.LotFull or ComplaintCategory.Damage => (ComplaintPriority.High, SupportTeam.Supervisor),
        ComplaintCategory.Overcharge or ComplaintCategory.RefundRequest or ComplaintCategory.AppError
            => (ComplaintPriority.Medium, SupportTeam.Support),
        _ => (ComplaintPriority.Low, SupportTeam.Support),
    };

    /// <summary>Khiếu nại liên quan tới bãi → chủ bãi phải phản hồi trong 48 giờ (lỗi app thì nền tảng tự xử lý).</summary>
    public static bool RequiresOwnerResponse(ComplaintCategory category)
        => category is not (ComplaintCategory.AppError or ComplaintCategory.Other);

    /// <summary>Booking chưa thanh toán thì chưa phát sinh dịch vụ để khiếu nại.</summary>
    public static bool CanComplainAbout(BookingStatus status)
        => status is not (BookingStatus.Created or BookingStatus.PendingPayment);

    /// <summary>Ticket còn đang xử lý → không cho mở ticket thứ hai cho cùng booking.</summary>
    public static bool IsOpen(ComplaintStatus status)
        => status is not (ComplaintStatus.Resolved or ComplaintStatus.Rejected);
}
