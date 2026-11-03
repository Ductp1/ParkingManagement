using System.Text.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Features.KybMisdeclaration;

/// <summary>
/// AuditRecorded của xử lý khai báo sai (US-056), EntityName = ParkingLot để lọc theo bãi ở Audit Log (US-100).
/// Ghi vào outbox cùng SaveChanges với dữ liệu nghiệp vụ. Case mới chưa có Id trước khi lưu → snapshot không chứa caseId.
/// </summary>
public static class MisdeclarationEvents
{
    public const string SourceService = "ParkingService";
    public const string EntityName = nameof(ParkingLot);

    public const string Warned = "MisdeclarationWarned";
    public const string Suspended = "MisdeclarationSuspended";
    public const string Resolved = "MisdeclarationResolved";
    public const string Reinstated = "Reinstated";
    public const string OwnerBanned = "OwnerBanned";
    /// <summary>Action trên KybApplication (EntityName = KybApplication): "KybApplication.RejectedByBan".</summary>
    public const string KybRejectedByBan = "RejectedByBan";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Ảnh chụp bãi + case (case = null khi chưa có case).</summary>
    public static object Snapshot(ParkingLot lot, LotMisdeclarationCase? c) => new
    {
        lotStatus = lot.Status.ToString(),
        caseId = c is { Id: > 0 } ? c.Id : (int?)null,
        caseStatus = c?.Status.ToString(),
        severity = c?.Severity.ToString(),
        category = c?.Category.ToString(),
        dueAtUtc = c?.DueAtUtc,
    };

    public static AuditRecorded Audit(ParkingLot lot, string action, int adminUserId, object before, object after, string? reason,
        DateTime nowUtc)
        => new(SourceService, adminUserId, $"{EntityName}.{action}", EntityName, lot.Id.ToString(),
            JsonSerializer.Serialize(before, Json), JsonSerializer.Serialize(after, Json), reason)
        {
            OccurredAtUtc = nowUtc,
        };
}
