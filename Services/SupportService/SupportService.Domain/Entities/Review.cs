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
    /// <summary>→ UserService (không FK).</summary>
    public int OwnerProfileId { get; set; }
    public byte Rating { get; set; }
    public string? Comment { get; set; }
    public string? OwnerReply { get; set; }
    public DateTime? OwnerRepliedAtUtc { get; set; }
    public bool IsFlagged { get; set; }
    public string? FlagReason { get; set; }
    public bool IsHidden { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

}
