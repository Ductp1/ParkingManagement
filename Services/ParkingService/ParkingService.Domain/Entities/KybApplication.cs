using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace ParkingService.Domain.Entities;

/// <summary>[ParkingService] Hồ sơ thẩm định KYB 4 bước (Nghiệp vụ v3 §3.4). UC-26, UC-39. Module: TV5.</summary>
public class KybApplication : BaseEntity
{
    public int ParkingLotId { get; set; }
    public KybStatus Status { get; set; } = KybStatus.Draft;

    // Bước 1 – Pháp lý
    public string? BusinessLicenseUrl { get; set; }
    // Bước 2 – Địa điểm (ảnh thực tế + GPS EXIF)
    public string? SitePhotoUrlsJson { get; set; }
    public double? PhotoLatitude { get; set; }
    public double? PhotoLongitude { get; set; }
    // Bước 3 – PCCC
    public string? FireSafetyCertificateUrl { get; set; }
    // Bước 4 – Khảo sát thực địa (bắt buộc với bãi > 50 chỗ)
    public bool FieldSurveyRequired { get; set; }
    public DateTime? FieldSurveyAtUtc { get; set; }
    public string? FieldSurveyNote { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }
    /// <summary>Admin đã duyệt / từ chối.</summary>
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? RejectReason { get; set; }

    public ParkingLot ParkingLot { get; set; } = null!;
}
