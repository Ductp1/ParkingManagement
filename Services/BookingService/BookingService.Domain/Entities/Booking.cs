using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace BookingService.Domain.Entities;

/// <summary>
/// [BookingService] Đặt chỗ – vòng đời 7 trạng thái (Đặc tả v3 §4.1). Module: TV3.
/// PlateNumber, ParkingLotName, SlotCode là bản chụp tại thời điểm đặt: tài xế đổi biển số
/// hay chủ bãi đổi tên bãi sau đó thì booking cũ vẫn giữ nguyên thông tin gốc.
/// </summary>
public class Booking : BaseEntity
{
    /// <summary>Mã booking cho khách và nhân viên cổng, VD "BK-20261002-0001".</summary>
    public string Code { get; set; } = string.Empty;

    // ===== Tài xế & xe =====
    public int UserId { get; set; }
    public int VehicleId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }

    // ===== Bãi & vị trí =====
    /// <summary>→ ParkingService (không FK).</summary>
    public int ParkingLotId { get; set; }
    /// <summary>Tenant của bãi – chủ bãi chỉ xem được booking có OwnerProfileId của mình.</summary>
    public int OwnerProfileId { get; set; }
    public string ParkingLotName { get; set; } = string.Empty;
    public int? ZoneId { get; set; }
    public int? SlotId { get; set; }
    public string? SlotCode { get; set; }
    public AllocationMode AllocationMode { get; set; } = AllocationMode.Dynamic;

    // ===== Thời gian =====
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    /// <summary>Hạn giữ chỗ 15 phút khi PendingPayment.</summary>
    public DateTime? HoldExpiresAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime? CheckedInAtUtc { get; set; }
    public DateTime? CheckedOutAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelReason { get; set; }
    public int? CancelledByUserId { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Created;
    /// <summary>Bãi Mức 0 – cần chủ bãi duyệt trong 10 phút.</summary>
    public bool RequiresOwnerApproval { get; set; }

    // ===== Tiền (giá đã khóa nằm ở PriceSnapshot, giao dịch nằm ở Payments) =====
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal PaidAmount { get; set; }
    /// <summary>→ PaymentService (không FK). PromotionCode là bản chụp.</summary>
    public int? PromotionId { get; set; }
    public string? PromotionCode { get; set; }

    /// <summary>Token có chữ ký để sinh mã QR check-in (hỗ trợ cả Offline Fallback).</summary>
    public string? QrToken { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public PriceSnapshot? PriceSnapshot { get; set; }
    public ICollection<BookingStatusLog> StatusLogs { get; set; } = new List<BookingStatusLog>();
    public ICollection<BookingModification> Modifications { get; set; } = new List<BookingModification>();

    // ===== Quản lý chuyển đổi trạng thái vòng đời đặt chỗ (Task T-301) =====

    /// <summary>Bắt đầu giữ chỗ miễn phí 15 phút khi vừa tạo đơn đặt chỗ (US-022).</summary>
    public void MarkAsPendingPayment(DateTime nowUtc)
    {
        if (Status != BookingStatus.Created)
            throw new ConflictException($"Không thể bắt đầu thanh toán từ trạng thái {Status}. Chỉ cho phép từ Created.");

        Status = BookingStatus.PendingPayment;
        HoldExpiresAtUtc = nowUtc.AddMinutes(15);

        AddStatusLog(BookingStatus.Created, BookingStatus.PendingPayment, UserId, "Bắt đầu giữ chỗ miễn phí 15 phút");
    }

    /// <summary>Chờ chủ bãi duyệt đơn đối với bãi thủ công Mức 0 (US-031).</summary>
    public void RequireOwnerApproval()
    {
        if (Status != BookingStatus.PendingPayment)
            throw new ConflictException($"Đơn đặt chỗ ở trạng thái {Status}, không thể yêu cầu chủ bãi duyệt (Cần PendingPayment).");

        RequiresOwnerApproval = true;
        Status = BookingStatus.PendingOwnerApproval;

        AddStatusLog(BookingStatus.PendingPayment, BookingStatus.PendingOwnerApproval, null, "Chờ chủ bãi kiểm tra và duyệt chỗ");
    }

    /// <summary>Chủ bãi duyệt đơn đặt chỗ đối với bãi thủ công Mức 0 (US-031). Không làm thay đổi PaidAmount.</summary>
    public void ApproveByOwner(DateTime confirmedAtUtc, int ownerUserId, string? reason = null)
    {
        if (Status != BookingStatus.PendingOwnerApproval && Status != BookingStatus.PendingPayment)
            throw new ConflictException($"Chỉ duyệt booking ở trạng thái chờ duyệt. Trạng thái hiện tại: {Status}");

        var oldStatus = Status;
        Status = BookingStatus.Confirmed;
        ConfirmedAtUtc = confirmedAtUtc;

        AddStatusLog(oldStatus, BookingStatus.Confirmed, ownerUserId, reason ?? "Chủ bãi duyệt thành công");
    }

    /// <summary>Xác nhận đặt chỗ thành công sau khi hoàn tất thanh toán tiền cọc/tiền đỗ.</summary>
    public void ConfirmPayment(decimal paidAmount, DateTime confirmedAtUtc)
    {
        if (Status != BookingStatus.PendingPayment && Status != BookingStatus.PendingOwnerApproval)
            throw new ConflictException($"Đơn đặt chỗ ở trạng thái {Status}, không thể xác nhận thanh toán.");

        var oldStatus = Status;
        Status = BookingStatus.Confirmed;
        PaidAmount = paidAmount;
        ConfirmedAtUtc = confirmedAtUtc;

        AddStatusLog(oldStatus, BookingStatus.Confirmed, UserId, "Thanh toán thành công, đơn đặt chỗ đã được xác nhận");
    }

    /// <summary>Chủ bãi từ chối duyệt đơn đối với bãi thủ công Mức 0 (US-031).</summary>
    public void RejectByOwner(DateTime cancelledAtUtc, int ownerUserId, string reason)
    {
        if (Status != BookingStatus.PendingOwnerApproval)
            throw new ConflictException($"Chỉ có thể từ chối booking khi đang ở trạng thái chờ duyệt (PendingOwnerApproval). Hiện tại: {Status}");

        var oldStatus = Status;
        Status = BookingStatus.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
        CancelledByUserId = ownerUserId;
        CancelReason = $"Chủ bãi từ chối: {reason}";

        AddStatusLog(oldStatus, BookingStatus.Cancelled, ownerUserId, CancelReason);
    }

    /// <summary>Ghi nhận xe vào bãi qua cổng barie (Check-in).</summary>
    public void CheckIn(DateTime checkedInAtUtc, int? staffUserId = null, int? gateDeviceId = null)
    {
        if (Status != BookingStatus.Confirmed)
            throw new ConflictException($"Đơn đặt chỗ ở trạng thái {Status}, không hợp lệ để vào bãi (Yêu cầu trạng thái Confirmed).");

        Status = BookingStatus.CheckedIn;
        CheckedInAtUtc = checkedInAtUtc;

        AddStatusLog(BookingStatus.Confirmed, BookingStatus.CheckedIn, staffUserId, $"Xác nhận xe vào bãi (Cổng {gateDeviceId})");
    }

    /// <summary>Ghi nhận xe rời bãi và hoàn tất phiên đỗ (Check-out).</summary>
    public void CheckOut(DateTime checkedOutAtUtc, int? staffUserId = null, int? gateDeviceId = null)
    {
        if (Status != BookingStatus.CheckedIn && Status != BookingStatus.Parking)
            throw new ConflictException($"Không thể hoàn tất do xe chưa vào bãi hoặc đã rời bãi (Trạng thái hiện tại: {Status}).");

        var oldStatus = Status;
        Status = BookingStatus.Completed;
        CheckedOutAtUtc = checkedOutAtUtc;
        CompletedAtUtc = checkedOutAtUtc;

        AddStatusLog(oldStatus, BookingStatus.Completed, staffUserId, $"Xác nhận xe ra bãi thành công (Cổng {gateDeviceId})");
    }

    /// <summary>Hết hạn thời gian giữ chỗ 15 phút (US-023).</summary>
    public void Expire(DateTime nowUtc)
    {
        if (Status != BookingStatus.PendingPayment)
            return;

        if (HoldExpiresAtUtc.HasValue && nowUtc < HoldExpiresAtUtc.Value)
            throw new ValidationException("Thời gian giữ chỗ vẫn còn hiệu lực.");

        Status = BookingStatus.Expired;
        AddStatusLog(BookingStatus.PendingPayment, BookingStatus.Expired, null, "Hết thời gian giữ chỗ 15 phút, tự động nhả vị trí đỗ");
    }

    /// <summary>Khách hàng hủy đơn trước khi vào bãi (US-038). Chỉ cho phép hủy từ các trạng thái chưa vào bãi.</summary>
    public void CancelByCustomer(BookingStatus targetStatus, DateTime cancelledAtUtc, int cancelledByUserId, string? reason)
    {
        if (Status != BookingStatus.Created &&
            Status != BookingStatus.PendingPayment &&
            Status != BookingStatus.PendingOwnerApproval &&
            Status != BookingStatus.Confirmed)
        {
            throw new ConflictException($"Không thể hủy đơn đặt chỗ khi đang ở trạng thái {Status}. Chỉ cho phép hủy từ Created, PendingPayment, PendingOwnerApproval hoặc Confirmed.");
        }

        var oldStatus = Status;
        Status = targetStatus;
        CancelledAtUtc = cancelledAtUtc;
        CancelledByUserId = cancelledByUserId;
        CancelReason = reason;

        AddStatusLog(oldStatus, targetStatus, cancelledByUserId, reason ?? "Khách hàng yêu cầu hủy đặt chỗ");
    }

    /// <summary>Chủ bãi hủy khẩn cấp do bãi gặp sự cố (US-042). Chỉ cho phép hủy từ các trạng thái chưa vào bãi.</summary>
    public void CancelByOwner(DateTime cancelledAtUtc, int ownerUserId, string reason)
    {
        if (Status != BookingStatus.Created &&
            Status != BookingStatus.PendingPayment &&
            Status != BookingStatus.PendingOwnerApproval &&
            Status != BookingStatus.Confirmed)
        {
            throw new ConflictException($"Chủ bãi không thể hủy đơn đặt chỗ khi đang ở trạng thái {Status}.");
        }

        var oldStatus = Status;
        Status = BookingStatus.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
        CancelledByUserId = ownerUserId;
        CancelReason = $"Chủ bãi hủy sự cố: {reason}";

        AddStatusLog(oldStatus, BookingStatus.Cancelled, ownerUserId, CancelReason);
    }

    /// <summary>Tự động xử lý khi khách không đến sau 30 phút giờ hẹn (US-039).</summary>
    public void MarkAsNoShow(DateTime nowUtc, int? staffUserId = null)
    {
        if (Status != BookingStatus.Confirmed)
            throw new ConflictException($"Đơn đặt chỗ ở trạng thái {Status}, không thể đánh dấu vắng mặt (Yêu cầu trạng thái Confirmed).");

        if (nowUtc < StartAtUtc.AddMinutes(30))
            throw new ValidationException("Chưa quá 30 phút từ giờ hẹn, không thể xử lý vắng mặt.");

        Status = BookingStatus.NoShow;
        CancelledAtUtc = nowUtc;
        CancelledByUserId = staffUserId;
        CancelReason = "Khách không đến sau 30 phút (Tự động hủy và khấu trừ tiền cọc)";

        AddStatusLog(BookingStatus.Confirmed, BookingStatus.NoShow, staffUserId, CancelReason);
    }

    private void AddStatusLog(BookingStatus? from, BookingStatus to, int? changedBy, string? reason)
    {
        StatusLogs.Add(new BookingStatusLog
        {
            BookingId = Id,
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = changedBy,
            Reason = reason
        });
    }
}
