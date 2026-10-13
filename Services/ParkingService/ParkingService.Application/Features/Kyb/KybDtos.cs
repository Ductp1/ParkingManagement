using ParkingManagement.SharedKernel.Enums;

namespace ParkingService.Application.Features.Kyb;

/// <summary>
/// DTO thông tin chi tiết hồ sơ thẩm định KYB 4 bước của bãi đỗ (US-054).
/// </summary>
public sealed record KybApplicationDto(
    int Id,
    int ParkingLotId,
    string Status,
    string? BusinessLicenseUrl,
    IReadOnlyList<string> SitePhotoUrls,
    double? PhotoLatitude,
    double? PhotoLongitude,
    string? FireSafetyCertificateUrl,
    bool FieldSurveyRequired,
    DateTime? FieldSurveyAtUtc,
    string? FieldSurveyNote,
    DateTime? SubmittedAtUtc,
    DateTime? ReviewedAtUtc,
    string? RejectReason);

/// <summary>Truy vấn hồ sơ KYB của bãi đỗ.</summary>
public sealed record GetKybApplicationQuery(
    int ParkingLotId,
    int OwnerProfileId);

/// <summary>Lệnh lưu nháp tiến độ hồ sơ KYB (cho phép lưu từng bước độc lập).</summary>
public sealed record SaveKybDraftCommand(
    int ParkingLotId,
    int OwnerProfileId,
    string? BusinessLicenseUrl = null,
    IReadOnlyList<string>? SitePhotoUrls = null,
    double? PhotoLatitude = null,
    double? PhotoLongitude = null,
    string? FireSafetyCertificateUrl = null,
    DateTime? FieldSurveyAtUtc = null,
    string? FieldSurveyNote = null);

/// <summary>Lệnh nộp chính thức hồ sơ KYB (yêu cầu kiểm tra toàn diện 4 bước).</summary>
public sealed record SubmitKybApplicationCommand(
    int ParkingLotId,
    int OwnerProfileId,
    string BusinessLicenseUrl,
    IReadOnlyList<string> SitePhotoUrls,
    double? PhotoLatitude,
    double? PhotoLongitude,
    string FireSafetyCertificateUrl,
    DateTime? FieldSurveyAtUtc,
    string? FieldSurveyNote);
