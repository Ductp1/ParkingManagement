using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace SupportService.Domain.Entities;

/// <summary>
/// [SupportService] Hội thoại trong 1 khiếu nại giữa tài xế – chủ bãi – Admin, kèm bằng chứng (US-088, US-089, US-090).
/// Thứ tự giá trị bằng chứng: thiết bị tự động/LPR &gt; GPS/biên lai của khách &gt; thao tác tay.
/// </summary>
public class ComplaintMessage : BaseEntity
{
    public int ComplaintId { get; set; }
    public ComplaintParty SenderParty { get; set; }
    /// <summary>→ UserService (không FK); null với tin nhắn hệ thống.</summary>
    public int? SenderUserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? AttachmentUrlsJson { get; set; }
    /// <summary>Ghi chú nội bộ của Admin, không hiển thị cho tài xế/chủ bãi.</summary>
    public bool IsInternal { get; set; }

    public Complaint Complaint { get; set; } = null!;
}
