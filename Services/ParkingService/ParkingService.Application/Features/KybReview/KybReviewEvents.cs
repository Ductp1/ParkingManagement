using System.Text.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.KybReview;

/// <summary>
/// Sự kiện tích hợp của thao tác Admin trên KYB (US-055). AuditRecorded đúng hợp đồng SharedKernel để AdminService (T-802)
/// lưu vào AuditLogs; ParkingLotApproved khi bãi được kích hoạt (activate-lot, sau khi ký hợp đồng bãi).
/// Ghi vào outbox cùng SaveChanges với dữ liệu nghiệp vụ.
/// </summary>
public static class KybReviewEvents
{
    public const string SourceService = "ParkingService";
    public const string EntityName = nameof(KybApplication);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Ảnh chụp các trường được audit của hồ sơ (gọi trước và sau khi đổi trạng thái).</summary>
    public static object Snapshot(KybApplication app) => new
    {
        status = app.Status.ToString(),
        lotStatus = app.ParkingLot?.Status.ToString(),
        fieldSurveyAtUtc = app.FieldSurveyAtUtc,
        fieldSurveyCompletedAtUtc = app.FieldSurveyCompletedAtUtc,
        reviewedByUserId = app.ReviewedByUserId,
    };

    public static AuditRecorded Audit(KybApplication app, string action, int adminUserId, object before, string? reason, DateTime nowUtc)
        => new(SourceService, adminUserId, $"{EntityName}.{action}", EntityName, app.Id.ToString(),
            JsonSerializer.Serialize(before, Json), JsonSerializer.Serialize(Snapshot(app), Json), reason)
        {
            OccurredAtUtc = nowUtc,
        };

    public static ParkingLotApproved LotApproved(ParkingLot lot, DateTime nowUtc)
        => new(lot.Id, lot.OwnerProfileId, lot.Name) { OccurredAtUtc = nowUtc };
}
