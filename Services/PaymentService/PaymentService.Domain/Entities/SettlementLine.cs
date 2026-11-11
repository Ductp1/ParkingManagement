using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace PaymentService.Domain.Entities;

/// <summary>
/// [PaymentService] Dòng chi tiết của kỳ quyết toán (US-080):
/// <list type="bullet">
/// <item>Booking / WalkIn: một giao dịch (Amount = số tiền gốc, CommissionAmount = Accrual) – mỗi giao dịch thuộc đúng 1 kỳ.</item>
/// <item>Refund: một lần hoàn (Amount âm); CommissionAmount âm = phần hoa hồng được giảm (gom vào dòng hoàn cuối của giao dịch trong kỳ).</item>
/// <item>DisputeHold: ReferenceCode = mã khiếu nại; Amount âm = giữ, dương = giải phóng.</item>
/// </list>
/// FinancialAdjustment không thành dòng (không có bãi) – gắn vào kỳ qua FinancialAdjustment.SettlementId.
/// </summary>
public class SettlementLine : BaseEntity
{
    public int SettlementId { get; set; }
    public SettlementLineType LineType { get; set; }
    public int? PaymentId { get; set; }
    public int? RefundId { get; set; }
    /// <summary>→ ParkingService (không FK).</summary>
    public int ParkingLotId { get; set; }
    /// <summary>Mã booking / mã giao dịch / mã khiếu nại – không chứa dữ liệu cá nhân của khách.</summary>
    public string? ReferenceCode { get; set; }
    public decimal Amount { get; set; }
    public decimal CommissionAmount { get; set; }
    /// <summary>Giao dịch chủ bãi tự thu (Cash / VietQR) – nền tảng không giữ tiền.</summary>
    public bool IsOwnerCollected { get; set; }
    /// <summary>Thời điểm phát sinh: ghi nhận giao dịch (booking kết thúc / thu tại cổng), hoàn tiền, hoặc lúc lấy khoản giữ.</summary>
    public DateTime? OccurredAtUtc { get; set; }
    /// <summary>Lý do dòng làm kỳ bị chặn gửi duyệt (CommissionPending, BookingNotFound…); null = bình thường.</summary>
    public string? BlockedReason { get; set; }

    public Settlement Settlement { get; set; } = null!;
    public Payment? Payment { get; set; }
    public Refund? Refund { get; set; }
}
