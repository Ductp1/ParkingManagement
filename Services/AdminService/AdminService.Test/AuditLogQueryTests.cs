using AdminService.Application.Features.AuditLogs;
using AdminService.Domain.Entities;
using AdminService.Infrastructure.Persistence.Queries;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-100: tra cứu audit log (unit test với fake IAuditLogQueries chạy đúng biểu thức lọc của AuditLogQueries trên LINQ in-memory).
public class AuditLogQueryTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);

    // Dữ liệu mẫu: log AdminService tự ghi (US-096, US-098, US-116) + log service khác gửi về qua AuditRecorded (T-802).
    private static readonly AuditLog OwnerLocked = AuditLogs.Create(1, Now.AddDays(-10), "AdminService", 1, 2, "Owner.Locked",
        "OwnerProfile", "12", "Gian lận doanh thu", "{\"isLocked\":false}", "{\"isLocked\":true}");
    private static readonly AuditLog ConfigScheduled = AuditLogs.Create(2, Now.AddDays(-5), "AdminService", 1, null,
        "SystemConfig.ChangeScheduled", "SystemConfig", "BOOKING_HOLD_MINUTES", "Thử nghiệm giữ chỗ dài hơn");
    private static readonly AuditLog PartnerContractSent = AuditLogs.Create(3, Now.AddDays(-2), "AdminService", 7, 5,
        "PartnerApplication.ContractSent", "PartnerApplication", "41", null);
    private static readonly AuditLog BookingCancelled = AuditLogs.Create(4, Now.AddDays(-1), "BookingService", 7, null,
        "Booking.Cancelled", "Booking", "BK-2026-00142", "Bãi báo đầy do sự cố cổng");
    private static readonly AuditLog KybApproved = AuditLogs.Create(5, Now.AddDays(-1), "ParkingService", 1, null,
        "KybApplication.Approved", "KybApplication", "9", "HD-SP-2026-052");

    private static readonly AuditLog[] All = [OwnerLocked, ConfigScheduled, PartnerContractSent, BookingCancelled, KybApproved];

    private static Task<PagedResult<AuditLogSummaryDto>> List(AuditLogListQuery query, int page = 1, int pageSize = 20)
        => new ListAuditLogsUseCase(new InMemoryAuditLogQueries(All)).ExecuteAsync(query, page, pageSize);

    [Fact]
    public async Task List_without_filter_returns_all_sources_newest_first_with_id_tiebreak()
    {
        var result = await List(new AuditLogListQuery());

        Assert.Equal([5, 4, 3, 2, 1], result.Items.Select(a => a.Id));   // #4 và #5 cùng thời điểm → Id lớn trước
        Assert.Equal((1, 20, 5), (result.Page, result.PageSize, result.TotalCount));
    }

    [Fact]
    public async Task List_item_carries_summary_fields_without_json_values()
    {
        var item = (await List(new AuditLogListQuery(EntityName: "OwnerProfile"))).Items.Single();

        Assert.Equal(new AuditLogSummaryDto(1, Now.AddDays(-10), "AdminService", 1, 2, "Owner.Locked", "OwnerProfile", "12",
            "Gian lận doanh thu"), item);
    }

    public static TheoryData<AuditLogListQuery, int[]> SingleFilters => new()
    {
        { new AuditLogListQuery(SourceService: "AdminService"), [3, 2, 1] },
        { new AuditLogListQuery(SourceService: "BookingService"), [4] },
        { new AuditLogListQuery(Action: "SystemConfig.ChangeScheduled"), [2] },
        { new AuditLogListQuery(EntityName: "KybApplication"), [5] },
        { new AuditLogListQuery(EntityName: "OwnerProfile", EntityId: "12"), [1] },
        { new AuditLogListQuery(EntityId: "BK-2026-00142"), [4] },
        { new AuditLogListQuery(UserId: 7), [4, 3] },
        { new AuditLogListQuery(OwnerProfileId: 5), [3] },
        { new AuditLogListQuery(From: Now.AddDays(-5)), [5, 4, 3, 2] },           // from: tính cả mốc bằng
        { new AuditLogListQuery(To: Now.AddDays(-5)), [1] },                      // to: không tính mốc bằng
        { new AuditLogListQuery(From: Now.AddDays(-6), To: Now.AddDays(-1)), [3, 2] },
    };

    [Theory]
    [MemberData(nameof(SingleFilters))]
    public async Task List_applies_each_filter(AuditLogListQuery query, int[] expectedIds)
    {
        var result = await List(query);

        Assert.Equal(expectedIds, result.Items.Select(a => a.Id));
        Assert.Equal(expectedIds.Length, result.TotalCount);
    }

    [Fact]
    public async Task List_combines_filters_with_and()
    {
        var result = await List(new AuditLogListQuery(SourceService: "AdminService", UserId: 1, From: Now.AddDays(-7)));

        Assert.Equal([2], result.Items.Select(a => a.Id));
    }

    [Fact]
    public async Task Filters_are_exact_match_so_other_case_does_not_match()
    {
        var result = await List(new AuditLogListQuery(SourceService: "adminservice"));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Theory]
    [InlineData("owner.LOCKED", new[] { 1 })]                   // Action
    [InlineData("systemconfig", new[] { 2 })]                   // EntityName + Action
    [InlineData("bk-2026", new[] { 4 })]                        // EntityId
    [InlineData("GIAN LẬN", new[] { 1 })]                       // Reason tiếng Việt, không phân biệt hoa thường
    [InlineData("7", new[] { 4, 3 })]                           // số → khớp UserId
    [InlineData("không-có", new int[0])]
    public async Task Search_matches_action_entity_reason_case_insensitive_and_user_id(string q, int[] expectedIds)
    {
        var result = await List(new AuditLogListQuery(Q: q));

        Assert.Equal(expectedIds, result.Items.Select(a => a.Id));
    }

    [Fact]
    public async Task Blank_text_filters_are_ignored_and_values_are_trimmed()
    {
        var blank = await List(new AuditLogListQuery(SourceService: "  ", Action: "", Q: " "));
        var trimmed = await List(new AuditLogListQuery(SourceService: "  BookingService "));

        Assert.Equal(5, blank.TotalCount);
        Assert.Equal([4], trimmed.Items.Select(a => a.Id));
    }

    [Fact]
    public async Task Time_without_kind_is_treated_as_utc_and_offset_is_converted()
    {
        var unspecified = DateTime.SpecifyKind(Now.AddDays(-5), DateTimeKind.Unspecified);
        var local = Now.AddDays(-5).ToLocalTime();

        Assert.Equal([5, 4, 3, 2], (await List(new AuditLogListQuery(From: unspecified))).Items.Select(a => a.Id));
        Assert.Equal([5, 4, 3, 2], (await List(new AuditLogListQuery(From: local))).Items.Select(a => a.Id));
    }

    [Fact]
    public async Task List_pages_after_sorting()
    {
        var page2 = await List(new AuditLogListQuery(), page: 2, pageSize: 2);
        var beyond = await List(new AuditLogListQuery(), page: 9, pageSize: 2);

        Assert.Equal([3, 2], page2.Items.Select(a => a.Id));
        Assert.Equal((2, 2, 5, 3), (page2.Page, page2.PageSize, page2.TotalCount, page2.TotalPages));
        Assert.Empty(beyond.Items);
        Assert.Equal(5, beyond.TotalCount);
    }

    [Fact]
    public async Task Range_of_exactly_366_days_is_allowed()
    {
        var result = await List(new AuditLogListQuery(From: Now.AddDays(-366), To: Now));

        Assert.Equal(5, result.TotalCount);
    }

    public static TheoryData<AuditLogListQuery, int, int> InvalidRequests => new()
    {
        { new AuditLogListQuery(), 0, 20 },
        { new AuditLogListQuery(), 1, 0 },
        { new AuditLogListQuery(), 1, 101 },
        { new AuditLogListQuery(), int.MaxValue, 100 },
        { new AuditLogListQuery(From: Now, To: Now), 1, 20 },
        { new AuditLogListQuery(From: Now, To: Now.AddDays(-1)), 1, 20 },
        { new AuditLogListQuery(From: Now.AddDays(-367), To: Now), 1, 20 },
        { new AuditLogListQuery(UserId: 0), 1, 20 },
        { new AuditLogListQuery(OwnerProfileId: -1), 1, 20 },
        { new AuditLogListQuery(SourceService: new string('x', 51)), 1, 20 },
        { new AuditLogListQuery(Action: new string('x', 101)), 1, 20 },
        { new AuditLogListQuery(EntityName: new string('x', 101)), 1, 20 },
        { new AuditLogListQuery(EntityId: new string('x', 51)), 1, 20 },
        { new AuditLogListQuery(Q: new string('x', 201)), 1, 20 },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Invalid_request_is_rejected_before_querying(AuditLogListQuery query, int page, int pageSize)
    {
        var queries = new InMemoryAuditLogQueries(All);

        await Assert.ThrowsAsync<ValidationException>(() => new ListAuditLogsUseCase(queries).ExecuteAsync(query, page, pageSize));
        Assert.Equal(0, queries.Calls);
    }

    [Fact]
    public async Task Use_case_passes_normalized_filter_to_queries()
    {
        var queries = new InMemoryAuditLogQueries(All);

        await new ListAuditLogsUseCase(queries).ExecuteAsync(
            new AuditLogListQuery(From: DateTime.SpecifyKind(Now, DateTimeKind.Unspecified), Action: " Owner.Locked ", Q: "  lock "), 3, 10);

        Assert.Equal(new AuditLogFilter(Now, null, null, "Owner.Locked", null, null, null, null, "lock"), queries.LastFilter);
        Assert.Equal(DateTimeKind.Utc, queries.LastFilter!.FromUtc!.Value.Kind);
        Assert.Equal((3, 10), queries.LastPaging);
    }

}

/// <summary>Dựng AuditLog cho test: Id và CreatedAtUtc của BaseEntity chỉ có setter protected (do database cấp).</summary>
internal static class AuditLogs
{
    public static AuditLog Create(int id, DateTime createdAtUtc, string sourceService, int? userId, int? ownerProfileId, string action,
        string entityName, string? entityId, string? reason, string? oldValuesJson = null, string? newValuesJson = null,
        string? ipAddress = null)
    {
        var log = new AuditLog
        {
            SourceService = sourceService,
            UserId = userId,
            OwnerProfileId = ownerProfileId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Reason = reason,
            OldValuesJson = oldValuesJson,
            NewValuesJson = newValuesJson,
            IpAddress = ipAddress,
        };
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(log, id);
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.CreatedAtUtc))!.SetValue(log, createdAtUtc);
        return log;
    }
}

/// <summary>Fake port: chạy đúng AuditLogQueryFilter (biểu thức EF dùng) trên danh sách in-memory.</summary>
internal sealed class InMemoryAuditLogQueries(IEnumerable<AuditLog> logs) : IAuditLogQueries
{
    public int Calls { get; private set; }
    public AuditLogFilter? LastFilter { get; private set; }
    public (int Page, int PageSize) LastPaging { get; private set; }

    public Task<PagedResult<AuditLogSummaryDto>> ListAsync(AuditLogFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        Calls++;
        LastFilter = filter;
        LastPaging = (page, pageSize);
        var query = AuditLogQueryFilter.Apply(logs.AsQueryable(), filter);
        var items = AuditLogQueryFilter.NewestFirst(query).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(AuditLogQueryFilter.ToSummary).ToList();
        return Task.FromResult(new PagedResult<AuditLogSummaryDto>(items, page, pageSize, query.Count()));
    }

    public Task<AuditLogRecord?> FindByIdAsync(int id, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(logs.AsQueryable().Where(a => a.Id == id).Select(AuditLogQueryFilter.ToRecord).FirstOrDefault());
    }

    public Task<AuditLogFacetCounts> CountByFacetAsync(CancellationToken cancellationToken)
    {
        Calls++;
        var query = logs.AsQueryable();
        return Task.FromResult(new AuditLogFacetCounts(AuditLogQueryFilter.CountBy(query, a => a.SourceService).ToList(),
            AuditLogQueryFilter.CountBy(query, a => a.Action).ToList(), AuditLogQueryFilter.CountBy(query, a => a.EntityName).ToList()));
    }

    public Task<IReadOnlyList<AuditLogExportRow>> ListForExportAsync(AuditLogFilter filter, int maxRows, CancellationToken cancellationToken)
    {
        Calls++;
        LastFilter = filter;
        IReadOnlyList<AuditLogExportRow> rows = AuditLogQueryFilter.NewestFirst(AuditLogQueryFilter.Apply(logs.AsQueryable(), filter))
            .Take(maxRows).Select(AuditLogQueryFilter.ToExportRow).ToList();
        return Task.FromResult(rows);
    }
}
