using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.AuditLogs;

// ===== DTO =====
/// <summary>US-100: một dòng audit log trong danh sách (giá trị cũ/mới xem ở chi tiết).</summary>
public sealed record AuditLogSummaryDto(int Id, DateTime CreatedAtUtc, string SourceService, int? UserId, int? OwnerProfileId,
    string Action, string EntityName, string? EntityId, string? Reason);

/// <summary>Tham số lọc nhận từ query string (chưa kiểm tra). from/to là UTC; q tìm trong Action, EntityName, EntityId, Reason, UserId.</summary>
public sealed record AuditLogListQuery(DateTime? From = null, DateTime? To = null, string? SourceService = null, string? Action = null,
    string? EntityName = null, string? EntityId = null, int? UserId = null, int? OwnerProfileId = null, string? Q = null);

/// <summary>Bộ lọc đã kiểm tra và chuẩn hóa: FromUtc ≤ CreatedAtUtc &lt; ToUtc; chuỗi đã trim, rỗng = không lọc.</summary>
public sealed record AuditLogFilter(DateTime? FromUtc, DateTime? ToUtc, string? SourceService, string? Action, string? EntityName,
    string? EntityId, int? UserId, int? OwnerProfileId, string? Search)
{
    public static AuditLogFilter None { get; } = new(null, null, null, null, null, null, null, null, null);
}

// ===== PORT (đọc) =====
public interface IAuditLogQueries
{
    /// <summary>Lọc + đếm + phân trang ở database, mới nhất trước (CreatedAtUtc giảm dần, Id giảm dần).</summary>
    Task<PagedResult<AuditLogSummaryDto>> ListAsync(AuditLogFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Một dòng đầy đủ theo Id (JSON gốc, chưa che); null nếu không có.</summary>
    Task<AuditLogRecord?> FindByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Số dòng theo từng SourceService / Action / EntityName đang có (GROUP BY ở database, dùng index).</summary>
    Task<AuditLogFacetCounts> CountByFacetAsync(CancellationToken cancellationToken);

    /// <summary>Tối đa maxRows dòng theo bộ lọc, mới nhất trước (xuất CSV).</summary>
    Task<IReadOnlyList<AuditLogExportRow>> ListForExportAsync(AuditLogFilter filter, int maxRows, CancellationToken cancellationToken);
}

/// <summary>US-100: kiểm tra tham số lọc dùng chung cho danh sách và xuất CSV.</summary>
public static class AuditLogFilterRules
{
    public const int MaxRangeDays = 366;
    public const int MaxSearchLength = 200;

    public static AuditLogFilter Normalize(AuditLogListQuery query)
    {
        var fromUtc = UtcDateTime.ToUtc(query.From);
        var toUtc = UtcDateTime.ToUtc(query.To);
        if (fromUtc is { } from && toUtc is { } to)
        {
            if (from >= to) throw new ValidationException("from phải nhỏ hơn to.");
            if (to - from > TimeSpan.FromDays(MaxRangeDays))
                throw new ValidationException($"Khoảng thời gian tra cứu tối đa {MaxRangeDays} ngày.");
        }
        if (query.UserId is <= 0) throw new ValidationException("userId phải là số nguyên dương.");
        if (query.OwnerProfileId is <= 0) throw new ValidationException("ownerProfileId phải là số nguyên dương.");

        return new AuditLogFilter(fromUtc, toUtc,
            Text(query.SourceService, "sourceService", 50),
            Text(query.Action, "action", 100),
            Text(query.EntityName, "entityName", 100),
            Text(query.EntityId, "entityId", 50),
            query.UserId, query.OwnerProfileId,
            Text(query.Q, "q", MaxSearchLength));
    }

    /// <summary>Trim; rỗng → null (không lọc); dài hơn cột trong AuditLogs → 400.</summary>
    private static string? Text(string? value, string name, int maxLength)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (trimmed is not null && trimmed.Length > maxLength)
            throw new ValidationException($"{name} tối đa {maxLength} ký tự.");
        return trimmed;
    }
}

// ===== USE CASE (đọc) =====
public interface IListAuditLogsUseCase
{
    Task<PagedResult<AuditLogSummaryDto>> ExecuteAsync(AuditLogListQuery query, int page, int pageSize,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-100 (UC-45) AC1: Admin tra cứu audit log toàn hệ thống theo thời gian, service, thao tác, đối tượng, người thực hiện, chủ bãi.
/// Gồm log AdminService tự ghi và log service khác gửi về (AuditRecorded – T-802). Chỉ đọc.
/// </summary>
public sealed class ListAuditLogsUseCase(IAuditLogQueries queries) : IListAuditLogsUseCase
{
    public Task<PagedResult<AuditLogSummaryDto>> ExecuteAsync(AuditLogListQuery query, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) throw new ValidationException("page phải ≥ 1.");
        if (pageSize is < 1 or > 100) throw new ValidationException("pageSize phải trong khoảng 1–100.");
        if ((long)(page - 1) * pageSize > int.MaxValue) throw new ValidationException("page quá lớn.");   // Skip của EF là int

        return queries.ListAsync(AuditLogFilterRules.Normalize(query), page, pageSize, cancellationToken);
    }
}
