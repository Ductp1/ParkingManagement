using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>
/// [PaymentService] Voucher đền bù cho tài xế: bãi hết chỗ dù đã Confirmed, bãi đóng khẩn cấp, Admin phán quyết tranh chấp
/// (US-091, UC-42, Answer_4 §22, Answer_6 §33). Nếu lỗi do bãi, giá trị voucher được trừ vào quyết toán của chủ bãi.
/// </summary>
public class CompensationVoucher : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    /// <summary>Tài xế nhận voucher (UserService, không FK).</summary>
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>Nguồn phát sinh: booking / khiếu nại (BookingService / SupportService, không FK).</summary>
    public int? BookingId { get; set; }
    public int? ComplaintId { get; set; }
    /// <summary>Chủ bãi chịu chi phí (null = nền tảng chịu).</summary>
    public int? ChargedToOwnerProfileId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public VoucherStatus Status { get; set; } = VoucherStatus.Issued;
    public DateTime? RedeemedAtUtc { get; set; }
    public int? RedeemedPaymentId { get; set; }
    public int IssuedByUserId { get; set; }

    public Payment? RedeemedPayment { get; set; }
}
