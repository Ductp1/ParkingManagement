using System.Text;
using AdminService.Application.Features.AuditLogs;
using AdminService.Domain.Entities;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-100: xuất audit log ra CSV (unit test với fake IAuditLogQueries, không cần database).
public class AuditLogExportTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 8, 5, 0, TimeSpan.Zero);
    private static readonly DateTime NowUtc = Now.UtcDateTime;

    private const string Header = "Id,CreatedAtUtc,CreatedAtIct,SourceService,UserId,OwnerProfileId,Action,EntityName,EntityId,Reason,IpAddress";

    private static readonly AuditLog OwnerLocked = AuditLogs.Create(1, new DateTime(2026, 10, 9, 2, 15, 0, DateTimeKind.Utc), "AdminService",
        1, 2, "Owner.Locked", "OwnerProfile", "12", "Gian lận doanh thu", "{\"isLocked\":false}", "{\"isLocked\":true}", "113.161.4.20");
    private static readonly AuditLog BookingCancelled = AuditLogs.Create(2, new DateTime(2026, 10, 19, 17, 30, 0, DateTimeKind.Utc),
        "BookingService", 7, null, "Booking.Cancelled", "Booking", "BK-2026-00142", "Bãi báo đầy, \"sự cố\" cổng\nca tối");

    private static ExportAuditLogsUseCase CreateUseCase(InMemoryAuditLogQueries queries) => new(queries, new FixedTimeProvider(Now));

    private static string[] Lines(AuditLogCsvFile file)
        => Encoding.UTF8.GetString(file.Content, 3, file.Content.Length - 3).Split("\r\n");

    [Fact]
    public async Task Export_writes_header_and_rows_newest_first_with_utc_and_ict_time()
    {
        var file = await CreateUseCase(new InMemoryAuditLogQueries([OwnerLocked, BookingCancelled])).ExecuteAsync(new AuditLogListQuery());

        var lines = Lines(file);
        Assert.Equal(Header, lines[0]);
        Assert.StartsWith("2,2026-10-19T17:30:00Z,20/10/2026 00:30:00,BookingService,7,,Booking.Cancelled,Booking,BK-2026-00142,", lines[1]);
        Assert.Equal("1,2026-10-09T02:15:00Z,09/10/2026 09:15:00,AdminService,1,2,Owner.Locked,OwnerProfile,12,Gian lận doanh thu,113.161.4.20",
            lines[2]);   // xuống dòng trong ô Reason là LF, nằm trong ngoặc kép; dòng CSV kết thúc bằng CRLF
        Assert.Equal("", lines[^1]);
    }

    [Fact]
    public async Task Export_quotes_cells_with_comma_quote_or_newline()
    {
        var file = await CreateUseCase(new InMemoryAuditLogQueries([BookingCancelled])).ExecuteAsync(new AuditLogListQuery());

        var text = Encoding.UTF8.GetString(file.Content);
        Assert.Contains(",\"Bãi báo đầy, \"\"sự cố\"\" cổng\nca tối\",", text);
    }

    [Fact]
    public async Task Export_is_utf8_with_bom_and_named_by_export_time()
    {
        var file = await CreateUseCase(new InMemoryAuditLogQueries([OwnerLocked])).ExecuteAsync(new AuditLogListQuery());

        Assert.Equal(Encoding.UTF8.GetPreamble(), file.Content[..3]);
        Assert.Equal("audit-logs-202610230805.csv", file.FileName);
        Assert.Equal("text/csv; charset=utf-8", AuditLogCsvFile.ContentType);
    }

    [Fact]
    public async Task Export_does_not_include_json_values()
    {
        var file = await CreateUseCase(new InMemoryAuditLogQueries([OwnerLocked])).ExecuteAsync(new AuditLogListQuery());

        Assert.DoesNotContain("isLocked", Encoding.UTF8.GetString(file.Content));
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")", "\"'=HYPERLINK(\"\"http://x\"\")\"")]
    [InlineData("+1+1", "'+1+1")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tcmd", "'\tcmd")]
    [InlineData("an toàn", "an toàn")]
    public async Task Export_neutralizes_formula_cells(string reason, string expectedCell)
    {
        var log = AuditLogs.Create(5, NowUtc, "UserService", 3, null, "User.Updated", "User", "3", reason);

        var file = await CreateUseCase(new InMemoryAuditLogQueries([log])).ExecuteAsync(new AuditLogListQuery());

        Assert.EndsWith($",{expectedCell},", Lines(file)[1]);
    }

    [Fact]
    public async Task Export_applies_same_filters_as_list()
    {
        var queries = new InMemoryAuditLogQueries([OwnerLocked, BookingCancelled]);

        var file = await CreateUseCase(queries).ExecuteAsync(new AuditLogListQuery(SourceService: " BookingService ", Q: "bk-2026"));

        Assert.Equal(new AuditLogFilter(null, null, "BookingService", null, null, null, null, null, "bk-2026"), queries.LastFilter);
        Assert.DoesNotContain("Owner.Locked", Encoding.UTF8.GetString(file.Content));
    }

    [Fact]
    public async Task Export_of_exactly_max_rows_succeeds()
    {
        var logs = Enumerable.Range(1, ExportAuditLogsUseCase.MaxRows)
            .Select(i => AuditLogs.Create(i, NowUtc.AddSeconds(-i), "AdminService", 1, null, "X.Y", "X", i.ToString(), null));

        var file = await CreateUseCase(new InMemoryAuditLogQueries(logs)).ExecuteAsync(new AuditLogListQuery());

        Assert.Equal(ExportAuditLogsUseCase.MaxRows + 2, Lines(file).Length);   // header + dòng dữ liệu + phần rỗng sau CRLF cuối
    }

    [Fact]
    public async Task Export_over_max_rows_is_rejected_instead_of_silently_truncated()
    {
        var logs = Enumerable.Range(1, ExportAuditLogsUseCase.MaxRows + 1)
            .Select(i => AuditLogs.Create(i, NowUtc.AddSeconds(-i), "AdminService", 1, null, "X.Y", "X", i.ToString(), null));

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase(new InMemoryAuditLogQueries(logs)).ExecuteAsync(new AuditLogListQuery()));
        Assert.Contains("thu hẹp bộ lọc", error.Message);
    }

    [Fact]
    public async Task Invalid_filter_is_rejected_before_querying()
    {
        var queries = new InMemoryAuditLogQueries([OwnerLocked]);

        await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase(queries).ExecuteAsync(new AuditLogListQuery(From: NowUtc, To: NowUtc.AddDays(-1))));
        Assert.Equal(0, queries.Calls);
    }

    [Fact]
    public async Task Export_of_no_rows_contains_only_header()
    {
        var file = await CreateUseCase(new InMemoryAuditLogQueries([])).ExecuteAsync(new AuditLogListQuery());

        Assert.Equal([Header, ""], Lines(file));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
