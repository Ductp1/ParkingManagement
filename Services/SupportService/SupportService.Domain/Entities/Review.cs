using ParkingManagement.SharedKernel.Domain;

namespace SupportService.Domain.Entities;

/// <summary>[SupportService] Đánh giá 1–5 sao, chỉ sau booking Completed, mỗi booking 1 lần (UC-18).</summary>
public class Review : BaseEntity, ISoftDelete
{
    public int BookingId { get; set; }
    /// <summary>→ UserService (không FK).</summary>
    public int UserId { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    /// <summary>→ ParkingService (không FK).</summary>
    public int ParkingLotId { get; set; }
    /// <summary>→ UserService (không FK). Null khi BookingService chưa trả về chủ bãi.</summary>
    public int? OwnerProfileId { get; set; }
    public byte Rating { get; set; }
    public string? Comment { get; set; }

    /// <summary>US-094 AC1: phản hồi công khai của chủ bãi – mỗi đánh giá 1 phản hồi.</summary>
    public string? OwnerReply { get; set; }
    public DateTime? OwnerRepliedAtUtc { get; set; }

    /// <summary>US-094 AC2: chủ bãi khiếu nại đánh giá → chờ Admin xem xét.</summary>
    public bool IsFlagged { get; set; }
    public string? FlagReason { get; set; }
    public DateTime? FlaggedAtUtc { get; set; }
    public int? FlaggedByOwnerProfileId { get; set; }

    /// <summary>US-094 AC2: chỉ Admin được gỡ (ẩn) đánh giá; đánh giá bị ẩn không còn tính vào điểm của bãi.</summary>
    public bool IsHidden { get; set; }
    public DateTime? HiddenAtUtc { get; set; }
    public int? HiddenByUserId { get; set; }
    public string? HiddenReason { get; set; }

    /// <summary>
    /// Optimistic concurrency: chủ bãi phản hồi / khiếu nại và Admin gỡ có thể cập nhật cùng đánh giá cùng lúc –
    /// bên lưu sau bị từ chối. PostgreSQL: ServiceDbContext tự đóng dấu ở SaveChanges.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    /// <summary>AC1: ghi phản hồi công khai của chủ bãi (chỉ 1 lần).</summary>
    public void ReplyAsOwner(string reply, DateTime nowUtc)
    {
        EnsureVisible();
        if (OwnerReply is not null) throw new InvalidOperationException("Đánh giá đã có phản hồi của chủ bãi.");
        OwnerReply = reply;
        OwnerRepliedAtUtc = nowUtc;
    }

    /// <summary>AC2: chủ bãi khiếu nại đánh giá → đưa vào hàng chờ Admin.</summary>
    public void ReportByOwner(int ownerProfileId, string reason, DateTime nowUtc)
    {
        EnsureVisible();
        if (IsFlagged) throw new InvalidOperationException("Đánh giá đang chờ Admin xem xét.");
        IsFlagged = true;
        FlagReason = reason;
        FlaggedAtUtc = nowUtc;
        FlaggedByOwnerProfileId = ownerProfileId;
    }

    /// <summary>AC2: Admin gỡ đánh giá (ẩn khỏi trang bãi và khỏi điểm trung bình); đóng luôn khiếu nại nếu có.</summary>
    public void HideByAdmin(int adminUserId, string reason, DateTime nowUtc)
    {
        EnsureVisible();
        IsHidden = true;
        HiddenAtUtc = nowUtc;
        HiddenByUserId = adminUserId;
        HiddenReason = reason;
        IsFlagged = false;
    }

    /// <summary>AC2: Admin bác khiếu nại – giữ nguyên đánh giá.</summary>
    public void DismissReport()
    {
        if (!IsFlagged) throw new InvalidOperationException("Đánh giá không có khiếu nại đang chờ xử lý.");
        IsFlagged = false;
    }

    private void EnsureVisible()
    {
        if (IsHidden) throw new InvalidOperationException("Đánh giá đã bị Admin gỡ.");
    }
}
