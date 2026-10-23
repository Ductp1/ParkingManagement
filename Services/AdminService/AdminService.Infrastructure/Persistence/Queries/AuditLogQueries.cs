using System.Globalization;
using System.Linq.Expressions;
using AdminService.Application.Features.AuditLogs;
using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Contracts;

namespace AdminService.Infrastructure.Persistence.Queries;

/// <summary>US-100: đọc audit log (AsNoTracking, lọc + đếm + Skip/Take ở PostgreSQL, Select thẳng sang DTO).</summary>
public sealed class AuditLogQueries(AdminDbContext db) : IAuditLogQueries
{
    public async Task<PagedResult<AuditLogSummaryDto>> ListAsync(AuditLogFilter filter, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var query = AuditLogQueryFilter.Apply(db.AuditLogs.AsNoTracking(), filter);
        var total = await query.CountAsync(cancellationToken);
        var items = await AuditLogQueryFilter.NewestFirst(query)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(AuditLogQueryFilter.ToSummary)
            .ToListAsync(cancellationToken);
        return new PagedResult<AuditLogSummaryDto>(items, page, pageSize, total);
    }

    public Task<AuditLogRecord?> FindByIdAsync(int id, CancellationToken cancellationToken)
        => db.AuditLogs.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(AuditLogQueryFilter.ToRecord)
            .FirstOrDefaultAsync(cancellationToken);
}

/// <summary>
/// Biểu thức lọc / sắp xếp / chiếu của AuditLogs. Chỉ dùng toán tử EF Core dịch được sang SQL và cũng chạy được trên LINQ in-memory
/// (unit test không cần database). Khớp chính xác các cột có index; q tìm không phân biệt hoa thường (lower(...) LIKE).
/// </summary>
public static class AuditLogQueryFilter
{
    public static readonly Expression<Func<AuditLog, AuditLogSummaryDto>> ToSummary = a => new AuditLogSummaryDto(a.Id, a.CreatedAtUtc,
        a.SourceService, a.UserId, a.OwnerProfileId, a.Action, a.EntityName, a.EntityId, a.Reason);

    public static readonly Expression<Func<AuditLog, AuditLogRecord?>> ToRecord = a => new AuditLogRecord(a.Id, a.CreatedAtUtc,
        a.SourceService, a.UserId, a.OwnerProfileId, a.Action, a.EntityName, a.EntityId, a.OldValuesJson, a.NewValuesJson, a.Reason,
        a.IpAddress);

    public static IQueryable<AuditLog> Apply(IQueryable<AuditLog> query, AuditLogFilter filter)
    {
        if (filter.FromUtc is { } from) query = query.Where(a => a.CreatedAtUtc >= from);
        if (filter.ToUtc is { } to) query = query.Where(a => a.CreatedAtUtc < to);
        if (filter.SourceService is { } source) query = query.Where(a => a.SourceService == source);
        if (filter.Action is { } action) query = query.Where(a => a.Action == action);
        if (filter.EntityName is { } entityName) query = query.Where(a => a.EntityName == entityName);
        if (filter.EntityId is { } entityId) query = query.Where(a => a.EntityId == entityId);
        if (filter.UserId is { } userId) query = query.Where(a => a.UserId == userId);
        if (filter.OwnerProfileId is { } ownerProfileId) query = query.Where(a => a.OwnerProfileId == ownerProfileId);
        if (filter.Search is { } search)
        {
            var term = search.ToLowerInvariant();
            int? searchUserId = int.TryParse(search, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
            query = query.Where(a => (searchUserId != null && a.UserId == searchUserId)
                                     || a.Action.ToLower().Contains(term)
                                     || a.EntityName.ToLower().Contains(term)
                                     || (a.EntityId != null && a.EntityId.ToLower().Contains(term))
                                     || (a.Reason != null && a.Reason.ToLower().Contains(term)));
        }
        return query;
    }

    /// <summary>Mới nhất trước; Id phá hòa khi trùng thời điểm để phân trang ổn định (index CreatedAtUtc, Id).</summary>
    public static IOrderedQueryable<AuditLog> NewestFirst(IQueryable<AuditLog> query)
        => query.OrderByDescending(a => a.CreatedAtUtc).ThenByDescending(a => a.Id);
}
