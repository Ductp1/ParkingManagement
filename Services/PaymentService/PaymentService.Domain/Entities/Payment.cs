using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>
/// [PaymentService] Giao dịch thanh toán: VNPAY/MoMo (online), VietQR/tiền mặt (tại cổng). UC-12, UC-50.
/// Gắn với 1 Booking (đặt chỗ, gia hạn) hoặc 1 ParkingSession (khách vãng lai, phụ thu tại cổng).
/// IdempotencyKey chống trừ tiền 2 lần khi callback gửi lặp.
/// </summary>
public class Payment : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    /// <summary>→ BookingService (không FK).</summary>
    public int? BookingId { get; set; }
    public string? BookingCode { get; set; }
    public int? ParkingSessionId { get; set; }
    /// <summary>null với khách vãng lai không có tài khoản.</summary>
    public int? UserId { get; set; }
    /// <summary>→ ParkingService (không FK).</summary>
    public int ParkingLotId { get; set; }
    /// <summary>→ UserService (không FK).</summary>
    public int OwnerProfileId { get; set; }

    public PaymentPurpose Purpose { get; set; } = PaymentPurpose.Booking;
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public decimal Amount { get; set; }
    public decimal RefundedAmount { get; set; }
    public string Currency { get; set; } = "VND";

    public string IdempotencyKey { get; set; } = string.Empty;
    /// <summary>Mã giao dịch của cổng thanh toán (vnp_TransactionNo...).</summary>
    public string? ProviderTransactionId { get; set; }
    public string? ProviderResponseCode { get; set; }
    public string? RawCallbackJson { get; set; }
    /// <summary>Nội dung chuyển khoản VietQR = mã lượt gửi.</summary>
    public string? TransferContent { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    /// <summary>Nhân viên xác nhận đã thu tiền mặt.</summary>
    public int? ConfirmedByStaffId { get; set; }
    public int RetryCount { get; set; }


    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();
    public ICollection<PaymentCallbackLog> CallbackLogs { get; set; } = new List<PaymentCallbackLog>();
    public Invoice? Invoice { get; set; }
}
