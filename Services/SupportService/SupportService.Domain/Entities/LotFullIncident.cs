using ParkingManagement.SharedKernel.Domain;
using SupportService.Domain.Rules;

namespace SupportService.Domain.Entities;

/// <summary>
/// [SupportService] Sự cố "khách đến nơi nhưng bãi hết chỗ" dù booking đã Confirmed (US-091). Luôn ưu tiên cao.
/// Vòng đời: Reported → TransferAccepted (AC2) | RefundRequested (AC3, kèm vi phạm SLA của bãi).
/// SupportService chỉ ghi nhận quyết định và phát sự kiện; chuyển booking (BookingService) và hoàn tiền + voucher
/// (PaymentService) do service sở hữu dữ liệu đó thực hiện. Mỗi booking chỉ có 1 sự cố (unique BookingId).
/// </summary>
public class LotFullIncident : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    /// <summary>Tài xế báo sự cố (lấy từ JWT).</summary>
    public int UserId { get; set; }
    /// <summary>→ BookingService (không FK) – bản chụp tại thời điểm báo.</summary>
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public int ParkingLotId { get; set; }
    public int? OwnerProfileId { get; set; }
    /// <summary>Số tiền tài xế đã trả cho booking lúc báo – số tiền dự kiến hoàn nếu từ chối bãi thay thế.</summary>
    public decimal PaidAmount { get; set; }

    public ComplaintPriority Priority { get; set; } = ComplaintPriority.High;
    public LotFullIncidentStatus Status { get; set; } = LotFullIncidentStatus.Reported;
    public string? Note { get; set; }
    /// <summary>Số chỗ trống bãi đang báo lên hệ thống lúc tài xế báo sự cố – bằng chứng khi đối soát với chủ bãi.</summary>
    public int? LotAvailableSlotsAtReport { get; set; }
    /// <summary>Danh sách bãi thay thế đã gợi ý (JSON) – tài xế chỉ được chọn trong danh sách này.</summary>
    public string AlternativesJson { get; set; } = "[]";

    public DateTime? DecidedAtUtc { get; set; }
    // AC2
    public int? NewParkingLotId { get; set; }
    public string? NewParkingLotName { get; set; }
    // AC3
    public int? RefundPercent { get; set; }
    public decimal? VoucherAmount { get; set; }
    public int? VoucherValidDays { get; set; }
    /// <summary>AC3: bãi bị ghi nhận vi phạm SLA (đã Confirmed nhưng không có chỗ, khách không nhận bãi thay thế).</summary>
    public bool IsSlaViolation { get; set; }

    public byte[] RowVersion { get; set; } = [];

    /// <summary>AC2: tài xế đồng ý chuyển miễn phí sang bãi thay thế.</summary>
    public void AcceptTransfer(int newParkingLotId, string newParkingLotName, DateTime nowUtc)
    {
        EnsureReported();
        Status = LotFullIncidentStatus.TransferAccepted;
        NewParkingLotId = newParkingLotId;
        NewParkingLotName = newParkingLotName;
        DecidedAtUtc = nowUtc;
    }

    /// <summary>AC3: tài xế không nhận bãi thay thế → yêu cầu hoàn tiền + voucher, ghi vi phạm SLA cho bãi.</summary>
    public void RequestRefund(int refundPercent, decimal voucherAmount, int voucherValidDays, DateTime nowUtc)
    {
        EnsureReported();
        Status = LotFullIncidentStatus.RefundRequested;
        RefundPercent = refundPercent;
        VoucherAmount = voucherAmount;
        VoucherValidDays = voucherValidDays;
        IsSlaViolation = true;
        DecidedAtUtc = nowUtc;
    }

    private void EnsureReported()
    {
        if (Status != LotFullIncidentStatus.Reported)
            throw new InvalidOperationException($"Sự cố {Code} đã được xử lý ({Status}).");
    }
}
