using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace ParkingService.Application.Abstractions;

/// <summary>Một dòng hồ sơ KYB kèm thông tin bãi cho màn danh sách Admin (US-055).</summary>
public sealed record AdminKybListRow(int Id, int ParkingLotId, string ParkingLotName, string ParkingLotAddress, string City,
    string? District, int OwnerProfileId, int TotalSlots, KybStatus Status, ParkingLotStatus LotStatus, DateTime? SubmittedAtUtc,
    bool FieldSurveyRequired, DateTime? FieldSurveyAtUtc, DateTime? FieldSurveyCompletedAtUtc, int? ReviewedByUserId,
    DateTime? ReviewedAtUtc);

/// <summary>Hồ sơ KYB đầy đủ + bãi PendingApproval + số ô đỗ đang hoạt động (dữ liệu thô, Application tự tính cảnh báo).</summary>
public sealed record AdminKybDetailRow(
    int Id, KybStatus Status, string? BusinessLicenseUrl, string? SitePhotoUrlsJson, double? PhotoLatitude, double? PhotoLongitude,
    string? FireSafetyCertificateUrl, bool FieldSurveyRequired, DateTime? FieldSurveyAtUtc, DateTime? FieldSurveyCompletedAtUtc,
    string? FieldSurveyNote, DateTime CreatedAtUtc, DateTime? SubmittedAtUtc, int? ReviewedByUserId, DateTime? ReviewedAtUtc,
    string? RejectReason, string? ReviewNote,
    int ParkingLotId, string LotName, string LotAddress, string LotCity, string? LotDistrict, double LotLatitude, double LotLongitude,
    int LotTotalSlots, int LotMaxHeightCm, TimeOnly LotOpenTime, TimeOnly LotCloseTime, ParkingLotStatus LotStatus,
    int LotOwnerProfileId, int ActiveSlotCount);

/// <summary>
/// PORT đọc (CQRS-lite) cho màn duyệt KYB của Admin (US-055): AsNoTracking + Select, tách khỏi cổng ghi
/// <see cref="IKybReviewRepository"/>.
/// </summary>
public interface IAdminKybQueries
{
    /// <summary>Lọc theo trạng thái, tìm theo tên / địa chỉ bãi (hoặc mã hồ sơ), mới nộp trước.</summary>
    Task<PagedResult<AdminKybListRow>> ListAsync(KybStatus? status, string? search, int page, int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminKybDetailRow?> GetDetailAsync(int id, CancellationToken cancellationToken = default);
}
