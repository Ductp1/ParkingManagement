using AdminService.Application.Features.Settings;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-098: lịch sử thay đổi của một tham số (unit test với fake ISystemConfigChangeQueries, không cần database).
public class SystemConfigHistoryTests
{
    private const string HoldKey = "BOOKING_HOLD_MINUTES";

    private static readonly DateTimeOffset Now = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTime NowUtc = Now.UtcDateTime;

    [Fact]
    public async Task History_lists_changes_newest_effective_date_first_with_status_and_previous_value()
    {
        var queries = new FakeQueries(History("15",
            Row(1, "20", NowUtc.AddDays(-5)),                                           // đã bị #2 thay thế
            Row(2, "25", NowUtc.AddDays(-1)),                                           // đang áp dụng
            Row(3, "30", NowUtc.AddDays(3)),                                            // đã lên lịch
            Row(4, "40", NowUtc.AddDays(1), cancelledAtUtc: NowUtc.AddHours(-2))));     // đã hủy

        var result = await CreateUseCase(queries).ExecuteAsync(HoldKey, 1, 20);

        Assert.Equal([3, 4, 2, 1], result.Items.Select(c => c.Id));
        Assert.Equal(["Scheduled", "Cancelled", "Effective", "Superseded"], result.Items.Select(c => c.Status));
        Assert.Equal(["25", "25", "20", "15"], result.Items.Select(c => c.PreviousValue));
        Assert.All(result.Items, c => Assert.Equal(HoldKey, c.Key));
        Assert.Equal((1, 20, 4), (result.Page, result.PageSize, result.TotalCount));
    }

    [Fact]
    public async Task History_item_carries_who_changed_why_and_cancel_details()
    {
        var queries = new FakeQueries(History("15",
            new SystemConfigChangeRow(4, "40", NowUtc.AddDays(1), "Thử nghiệm giờ cao điểm", CreatedByUserId: 1, NowUtc.AddDays(-3),
                CancelledAtUtc: NowUtc.AddHours(-2), CancelledByUserId: 9, CancelReason: "Nhập nhầm")));

        var item = Assert.Single((await CreateUseCase(queries).ExecuteAsync(HoldKey, 1, 20)).Items);

        Assert.Equal(new SystemConfigChangeDto(4, HoldKey, "40", "15", NowUtc.AddDays(1), "Cancelled", "Thử nghiệm giờ cao điểm", 1,
            NowUtc.AddDays(-3), NowUtc.AddHours(-2), 9, "Nhập nhầm"), item);
    }

    [Fact]
    public async Task Change_effective_exactly_now_is_effective()
    {
        var queries = new FakeQueries(History("15", Row(1, "20", NowUtc)));

        var item = Assert.Single((await CreateUseCase(queries).ExecuteAsync(HoldKey, 1, 20)).Items);

        Assert.Equal("Effective", item.Status);
    }

    [Fact]
    public async Task Cancelled_change_does_not_supersede_or_feed_previous_value_of_others()
    {
        var queries = new FakeQueries(History("15",
            Row(1, "20", NowUtc.AddDays(-5)),
            Row(2, "99", NowUtc.AddDays(-1), cancelledAtUtc: NowUtc.AddDays(-2)),
            Row(3, "30", NowUtc.AddDays(3))));

        var result = await CreateUseCase(queries).ExecuteAsync(HoldKey, 1, 20);

        var byId = result.Items.ToDictionary(c => c.Id);
        Assert.Equal("Effective", byId[1].Status);                       // #2 đã hủy nên #1 vẫn đang áp dụng
        Assert.Equal("Cancelled", byId[2].Status);
        Assert.Equal("20", byId[2].PreviousValue);
        Assert.Equal("20", byId[3].PreviousValue);                       // bỏ qua giá trị 99 của thay đổi đã hủy
    }

    [Fact]
    public async Task Change_rescheduled_on_a_cancelled_effective_date_is_listed_before_the_cancelled_one()
    {
        var effectiveFrom = NowUtc.AddDays(2);
        var queries = new FakeQueries(History("15",
            Row(1, "20", effectiveFrom, cancelledAtUtc: NowUtc.AddHours(-1)),
            Row(2, "25", effectiveFrom)));

        var result = await CreateUseCase(queries).ExecuteAsync(HoldKey, 1, 20);

        Assert.Equal([2, 1], result.Items.Select(c => c.Id));
        Assert.Equal(["Scheduled", "Cancelled"], result.Items.Select(c => c.Status));
        Assert.Equal(["15", "15"], result.Items.Select(c => c.PreviousValue));
    }

    [Fact]
    public async Task History_is_paged_after_status_is_computed_on_the_whole_timeline()
    {
        var queries = new FakeQueries(History("15",
            Row(1, "20", NowUtc.AddDays(-5)), Row(2, "25", NowUtc.AddDays(-1)), Row(3, "30", NowUtc.AddDays(3))));

        var page2 = await CreateUseCase(queries).ExecuteAsync(HoldKey, 2, 2);

        var item = Assert.Single(page2.Items);
        Assert.Equal(1, item.Id);
        Assert.Equal("Superseded", item.Status);                         // biết bị thay thế nhờ dòng nằm ở trang 1
        Assert.Equal((2, 2, 3, 2), (page2.Page, page2.PageSize, page2.TotalCount, page2.TotalPages));
    }

    [Fact]
    public async Task Page_beyond_the_last_one_is_empty_but_keeps_total_count()
    {
        var queries = new FakeQueries(History("15", Row(1, "20", NowUtc.AddDays(-5))));

        var result = await CreateUseCase(queries).ExecuteAsync(HoldKey, 5, 20);

        Assert.Empty(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Huge_page_number_is_empty_instead_of_wrapping_around()
    {
        // (page - 1) * pageSize vượt int: không được tràn số rồi trả nhầm dữ liệu trang đầu.
        var queries = new FakeQueries(History("15", Row(1, "20", NowUtc.AddDays(-5))));

        var result = await CreateUseCase(queries).ExecuteAsync(HoldKey, int.MaxValue, 100);

        Assert.Empty(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Config_never_changed_has_empty_history()
    {
        var result = await CreateUseCase(new FakeQueries(History("15"))).ExecuteAsync(HoldKey, 1, 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Key_is_matched_ignoring_case_and_surrounding_spaces()
    {
        var queries = new FakeQueries(History("15"));

        await CreateUseCase(queries).ExecuteAsync("  booking_hold_minutes ", 1, 20);

        Assert.Equal(HoldKey, queries.RequestedKey);
    }

    [Fact]
    public async Task Unknown_key_throws_not_found()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => CreateUseCase(new FakeQueries(History("15"))).ExecuteAsync("hold_ttl", 1, 20));

        Assert.Contains("Tham số hệ thống", ex.Message);
        Assert.Contains("HOLD_TTL", ex.Message);
    }

    [Theory]
    [InlineData(0, 20, "page phải ≥ 1.")]
    [InlineData(-1, 20, "page phải ≥ 1.")]
    [InlineData(1, 0, "pageSize phải trong khoảng 1–100.")]
    [InlineData(1, 101, "pageSize phải trong khoảng 1–100.")]
    public async Task Invalid_paging_is_rejected_before_reading(int page, int pageSize, string expectedMessage)
    {
        var queries = new FakeQueries(History("15"));

        var ex = await Assert.ThrowsAsync<ValidationException>(() => CreateUseCase(queries).ExecuteAsync(HoldKey, page, pageSize));

        Assert.Equal(expectedMessage, ex.Message);
        Assert.Null(queries.RequestedKey);
    }

    private static GetSystemConfigHistoryUseCase CreateUseCase(FakeQueries queries) => new(queries, new FixedTimeProvider(Now));

    private static SystemConfigHistoryRecord History(string defaultValue, params SystemConfigChangeRow[] rows) => new(HoldKey, defaultValue, rows);

    private static SystemConfigChangeRow Row(int id, string value, DateTime effectiveFromUtc, DateTime? cancelledAtUtc = null)
        => new(id, value, effectiveFromUtc, "Lý do", 1, NowUtc.AddDays(-10), cancelledAtUtc,
            cancelledAtUtc is null ? null : 1, cancelledAtUtc is null ? null : "Lý do hủy");

    private sealed class FakeQueries(SystemConfigHistoryRecord history) : ISystemConfigChangeQueries
    {
        public string? RequestedKey { get; private set; }

        // Khớp chính xác như so sánh chuỗi của PostgreSQL.
        public Task<SystemConfigHistoryRecord?> FindHistoryByKeyAsync(string key, CancellationToken cancellationToken)
        {
            RequestedKey = key;
            return Task.FromResult(string.Equals(history.Key, key, StringComparison.Ordinal) ? history : null);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
