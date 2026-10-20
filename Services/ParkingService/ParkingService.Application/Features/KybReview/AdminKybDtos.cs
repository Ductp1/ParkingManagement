namespace ParkingService.Application.Features.KybReview;

// ===== DTO trả về (US-055) =====

/// <summary>Dòng danh sách hồ sơ KYB cho Admin.</summary>
public sealed record AdminKybApplicationSummaryDto(int Id, int ParkingLotId, string ParkingLotName, string ParkingLotAddress,
    string City, string? District, int OwnerProfileId, int TotalSlots, string Status, string LotStatus, DateTime? SubmittedAtUtc,
    bool FieldSurveyRequired, DateTime? FieldSurveyAtUtc, DateTime? FieldSurveyCompletedAtUtc, int? ReviewedByUserId,
    DateTime? ReviewedAtUtc);

/// <summary>Thông tin bãi đang chờ duyệt hiển thị cạnh hồ sơ.</summary>
public sealed record AdminKybLotDto(int Id, string Name, string Address, string City, string? District, double Latitude,
    double Longitude, int TotalSlots, int MaxHeightCm, TimeOnly OpenTime, TimeOnly CloseTime, string Status, int OwnerProfileId,
    int ActiveSlotCount);

/// <summary>
/// Chi tiết hồ sơ KYB 4 phần: pháp lý, ảnh hiện trường + GPS, PCCC, khảo sát thực địa.
/// GpsDistanceMeters / GpsWarning: so tọa độ ảnh với tọa độ bãi. ApprovalBlockers: lý do chưa thể duyệt (rỗng = duyệt được).
/// </summary>
public sealed record AdminKybApplicationDetailDto(
    int Id, string Status, string? BusinessLicenseUrl, IReadOnlyList<string> SitePhotoUrls, double? PhotoLatitude,
    double? PhotoLongitude, double? GpsDistanceMeters, bool GpsWarning, string? FireSafetyCertificateUrl, bool FieldSurveyRequired,
    bool FieldSurveyMandatory, DateTime? FieldSurveyAtUtc, DateTime? FieldSurveyCompletedAtUtc, string? FieldSurveyNote,
    DateTime CreatedAtUtc, DateTime? SubmittedAtUtc, int? ReviewedByUserId, DateTime? ReviewedAtUtc, string? RejectReason,
    string? ReviewNote, IReadOnlyList<string> ApprovalBlockers, AdminKybLotDto ParkingLot);

// ===== Request (người thao tác lấy từ JWT, không nằm trong body) =====

public sealed record ScheduleKybFieldSurveyRequest(DateTime? FieldSurveyAtUtc, string? Note);

public sealed record RecordKybFieldSurveyResultRequest(string? Note);

public sealed record ApproveKybApplicationRequest(string? Note);

public sealed record RejectKybApplicationRequest(string? Reason);

public sealed record RequestKybMoreInfoRequest(string? Note);

/// <summary>activate-lot (AdminService gọi sau khi hợp đồng bãi được ký): số hợp đồng ghi vào AuditRecorded.Reason.</summary>
public sealed record ActivateKybLotRequest(string? ContractNumber);
