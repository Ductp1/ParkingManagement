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

    /// <summary>US-089: phản hồi của chủ bãi (cột OwnerResponse 4000 ký tự) và tối đa 10 bằng chứng (log check-in/out, ảnh camera...).</summary>
    public const int MinOwnerResponseLength = 10;
    public const int MaxOwnerResponseLength = 4000;
    public const int MaxOwnerEvidenceFiles = 10;

    /// <summary>
    /// US-089 AC3: khiếu nại về bãi có đòi tiền → phong tỏa khoản quyết toán của chủ bãi (hold payout) đến khi có phán quyết.
    /// Phải biết chủ bãi (khoản giữ gắn với chủ bãi nào); lỗi ứng dụng / khác do nền tảng xử lý nên không giữ tiền chủ bãi.
    /// </summary>
    public static bool ShouldHoldPayout(ComplaintCategory category, decimal? requestedAmount, int? ownerProfileId)
        => ownerProfileId is not null && RequiresOwnerResponse(category) && requestedAmount > 0;

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

    /// <summary>Booking đang diễn ra (đã xác nhận, đã vào cổng, đang đỗ, chờ chủ bãi duyệt) – chưa có kết thúc.</summary>
    public static bool IsOngoing(BookingStatus status)
        => status is BookingStatus.Confirmed or BookingStatus.CheckedIn or BookingStatus.Parking or BookingStatus.PendingOwnerApproval;

    /// <summary>
    /// Mốc sự cố dùng để tính hạn khiếu nại, theo trạng thái booking:
    /// đang diễn ra → bây giờ, nhưng không muộn hơn giờ kết thúc dự kiến (booking "Confirmed" đã quá giờ mà chưa được
    /// chuyển no-show vẫn bị tính hạn từ giờ kết thúc); đã kết thúc (hoàn tất, hủy, từ chối, hết hạn, no-show) →
    /// thời điểm đổi trạng thái cuối, tức lúc check-out thực tế hoặc lúc có quyết định; không có lịch sử thì lấy giờ kết thúc dự kiến.
    /// </summary>
    public static DateTime ResolveIncidentAtUtc(BookingStatus status, DateTime endAtUtc, DateTime? lastChangedAtUtc, DateTime nowUtc)
        => IsOngoing(status) ? (endAtUtc < nowUtc ? endAtUtc : nowUtc) : lastChangedAtUtc ?? endAtUtc;

    /// <summary>Ticket còn đang xử lý → không cho mở ticket thứ hai cho cùng booking.</summary>
    public static bool IsOpen(ComplaintStatus status)
        => status is not (ComplaintStatus.Resolved or ComplaintStatus.Rejected);
}
