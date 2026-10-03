using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>[PaymentService] Hoàn tiền: khách hủy, lỗi bãi, Admin override khi tranh chấp (UC-16, UC-36, UC-42).</summary>
public class Refund : BaseEntity
{
    public int PaymentId { get; set; }
    public decimal Amount { get; set; }
    public RefundReason Reason { get; set; }
    public string? Note { get; set; }
    public RefundStatus Status { get; set; } = RefundStatus.Requested;
    /// <summary>Hoàn tiền phát sinh từ khiếu nại (nếu có).</summary>
    public int? ComplaintId { get; set; }
    public int? RequestedByUserId { get; set; }
    public int? ApprovedByUserId { get; set; }
    public string? ProviderRefundId { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }

    public Payment Payment { get; set; } = null!;
}
