using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

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

    public uint RowVersion { get; set; }   // Postgres: ánh xạ sang cột hệ thống xmin (xem BookingConfiguration)


    public PriceSnapshot? PriceSnapshot { get; set; }
    public ICollection<BookingStatusLog> StatusLogs { get; set; } = new List<BookingStatusLog>();
    public ICollection<BookingModification> Modifications { get; set; } = new List<BookingModification>();
    /// <summary>Đặt chỗ, gia hạn, phụ thu quá giờ – mỗi lần là 1 Payment.</summary>
}
