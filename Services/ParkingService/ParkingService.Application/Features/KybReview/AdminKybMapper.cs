using System.Text.Json;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Abstractions;
using ParkingService.Domain.Entities;
using ParkingService.Domain.Services;

namespace ParkingService.Application.Features.KybReview;

/// <summary>Chuyển dữ liệu thô của cổng đọc sang DTO Admin, kèm các phép tính nghiệp vụ hiển thị (GPS, điều kiện duyệt).</summary>
public static class AdminKybMapper
{
    /// <summary>Ảnh hiện trường lệch quá ngưỡng này so với tọa độ bãi → cảnh báo cho Admin (US-054 đã chặn nộp khi &gt; 500 m).</summary>
    public const double GpsWarningMeters = 100;

    public static AdminKybApplicationSummaryDto ToDto(this AdminKybListRow r) => new(
        r.Id, r.ParkingLotId, r.ParkingLotName, r.ParkingLotAddress, r.City, r.District, r.OwnerProfileId, r.TotalSlots,
        r.Status.ToString(), r.LotStatus.ToString(), r.SubmittedAtUtc, r.FieldSurveyRequired, r.FieldSurveyAtUtc,
        r.FieldSurveyCompletedAtUtc, r.ReviewedByUserId, r.ReviewedAtUtc);

    public static AdminKybApplicationDetailDto ToDto(this AdminKybDetailRow r)
    {
        double? distance = r is { PhotoLatitude: { } lat, PhotoLongitude: { } lng }
            ? Math.Round(GeoDistance.HaversineKm(lat, lng, r.LotLatitude, r.LotLongitude) * 1000, 1)
            : null;
        var mandatory = KybApplication.IsFieldSurveyMandatory(r.LotTotalSlots);

        var lot = new AdminKybLotDto(r.ParkingLotId, r.LotName, r.LotAddress, r.LotCity, r.LotDistrict, r.LotLatitude, r.LotLongitude,
            r.LotTotalSlots, r.LotMaxHeightCm, r.LotOpenTime, r.LotCloseTime, r.LotStatus.ToString(), r.LotOwnerProfileId,
            r.ActiveSlotCount);

        return new AdminKybApplicationDetailDto(
            r.Id, r.Status.ToString(), r.BusinessLicenseUrl, ParsePhotos(r.SitePhotoUrlsJson), r.PhotoLatitude, r.PhotoLongitude,
            distance, distance is null || distance > GpsWarningMeters, r.FireSafetyCertificateUrl, r.FieldSurveyRequired, mandatory,
            r.FieldSurveyAtUtc, r.FieldSurveyCompletedAtUtc, r.FieldSurveyNote, r.CreatedAtUtc, r.SubmittedAtUtc, r.ReviewedByUserId,
            r.ReviewedAtUtc, r.RejectReason, r.ReviewNote, ApprovalBlockers(r, mandatory), lot);
    }

    /// <summary>Các điều kiện duyệt còn thiếu (cùng điều kiện mà KybApplication.Approve kiểm tra).</summary>
    private static IReadOnlyList<string> ApprovalBlockers(AdminKybDetailRow r, bool surveyMandatory)
    {
        var blockers = new List<string>();
        if (r.Status != KybStatus.UnderReview)
            blockers.Add($"Hồ sơ đang ở trạng thái {r.Status}, chỉ duyệt được khi UnderReview.");
        if (r.LotStatus != ParkingLotStatus.PendingApproval)
            blockers.Add($"Bãi đang ở trạng thái {r.LotStatus}, không phải chờ duyệt.");
        if (surveyMandatory && r.FieldSurveyCompletedAtUtc is null)
            blockers.Add($"Bãi trên {KybApplication.FieldSurveySlotThreshold} chỗ chưa được khảo sát thực địa.");
        if (r.ActiveSlotCount <= 0)
            blockers.Add("Bãi chưa có sơ đồ ô đỗ đang hoạt động.");
        return blockers;
    }

    private static IReadOnlyList<string> ParsePhotos(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
