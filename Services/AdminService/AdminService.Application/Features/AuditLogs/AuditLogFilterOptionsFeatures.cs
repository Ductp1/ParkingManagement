namespace AdminService.Application.Features.AuditLogs;

// ===== DTO =====
/// <summary>Một giá trị đang có trong AuditLogs và số dòng mang giá trị đó.</summary>
public sealed record AuditLogFacetDto(string Value, int Count);

/// <summary>Giá trị thô theo từng cột lọc, đếm ở database (GROUP BY) – chỉ dùng giữa port đọc và use case.</summary>
public sealed record AuditLogFacetCounts(IReadOnlyList<AuditLogFacetDto> SourceServices, IReadOnlyList<AuditLogFacetDto> Actions,
    IReadOnlyList<AuditLogFacetDto> EntityNames);

/// <summary>US-100: giá trị cho dropdown / facet lọc của màn Audit Log (Figma: Source service + số đếm, "All" = TotalCount).</summary>
public sealed record AuditLogFilterOptionsDto(int TotalCount, IReadOnlyList<AuditLogFacetDto> SourceServices,
    IReadOnlyList<AuditLogFacetDto> Actions, IReadOnlyList<AuditLogFacetDto> EntityNames);

// ===== USE CASE (đọc) =====
public interface IGetAuditLogFilterOptionsUseCase
{
    Task<AuditLogFilterOptionsDto> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// US-100: SourceService / Action / EntityName đang có trong AuditLogs kèm số dòng, xếp theo tên (ordinal) cho dropdown ổn định.
/// Có cả service khác khi T-802 ghi log về; không hard-code danh sách service.
/// </summary>
public sealed class GetAuditLogFilterOptionsUseCase(IAuditLogQueries queries) : IGetAuditLogFilterOptionsUseCase
{
    public async Task<AuditLogFilterOptionsDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var facets = await queries.CountByFacetAsync(cancellationToken);
        return new AuditLogFilterOptionsDto(facets.SourceServices.Sum(f => f.Count), Sorted(facets.SourceServices),
            Sorted(facets.Actions), Sorted(facets.EntityNames));
    }

    private static IReadOnlyList<AuditLogFacetDto> Sorted(IEnumerable<AuditLogFacetDto> facets)
        => [.. facets.OrderBy(f => f.Value, StringComparer.Ordinal)];
}
