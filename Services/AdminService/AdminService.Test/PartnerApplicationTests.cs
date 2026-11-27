using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-116: Partner Onboarding – bảng chuyển trạng thái (một chỗ kiểm tra) + danh sách / chi tiết hồ sơ (fake IPartnerApplicationQueries).
public class PartnerApplicationTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 2, 0, 0, DateTimeKind.Utc);

    private static PartnerApplication Application(PartnerApplicationStatus status)
    {
        var app = PartnerApplication.Submit("PA-2026-0012", Now);
        var path = status switch
        {
            PartnerApplicationStatus.Submitted => [],
            PartnerApplicationStatus.UnderReview => [PartnerApplicationStatus.UnderReview],
            PartnerApplicationStatus.NeedsMoreInfo => [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.NeedsMoreInfo],
            PartnerApplicationStatus.AccountIssued => [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.AccountIssued],
            PartnerApplicationStatus.Activated =>
            [
                PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.AccountIssued, PartnerApplicationStatus.Activated,
            ],
            PartnerApplicationStatus.Rejected => [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.Rejected],
            PartnerApplicationStatus.Cancelled => [PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.Cancelled],
            _ => Array.Empty<PartnerApplicationStatus>(),
        };
        foreach (var step in path) app.MoveTo(step);
        return app;
    }

    // ==================== Bảng chuyển trạng thái (design mục 14.2 – hợp đồng ký theo từng bãi) ====================

    public static TheoryData<PartnerApplicationStatus, PartnerApplicationStatus> ValidTransitions => new()
    {
        { PartnerApplicationStatus.Submitted, PartnerApplicationStatus.UnderReview },            // T2
        { PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.NeedsMoreInfo },        // T3
        { PartnerApplicationStatus.NeedsMoreInfo, PartnerApplicationStatus.UnderReview },        // T4
        { PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.AccountIssued },        // T6
        { PartnerApplicationStatus.AccountIssued, PartnerApplicationStatus.AccountIssued },      // T7
        { PartnerApplicationStatus.AccountIssued, PartnerApplicationStatus.Activated },          // T8
        { PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.Rejected },             // T9
        { PartnerApplicationStatus.UnderReview, PartnerApplicationStatus.Cancelled },            // T10
        { PartnerApplicationStatus.NeedsMoreInfo, PartnerApplicationStatus.Cancelled },          // T10
    };

    [Theory]
    [MemberData(nameof(ValidTransitions))]
    public void Valid_transition_is_allowed(PartnerApplicationStatus from, PartnerApplicationStatus to)
    {
        var app = Application(from);

        app.MoveTo(to);

        Assert.Equal(to, app.Status);
    }

    [Fact]
    public void Every_transition_outside_the_table_is_rejected_with_409()
    {
        var valid = ValidTransitions.Select(row => ((PartnerApplicationStatus)row[0], (PartnerApplicationStatus)row[1])).ToHashSet();
        var all = Enum.GetValues<PartnerApplicationStatus>();

        foreach (var from in all)
        foreach (var to in all)
        {
            Assert.Equal(valid.Contains((from, to)), PartnerApplication.CanMove(from, to));
            if (valid.Contains((from, to))) continue;
            var app = Application(from);
            var ex = Assert.Throws<ConflictException>(() => app.MoveTo(to));
            Assert.Contains("PA-2026-0012", ex.Message);
            Assert.Equal(from, app.Status);   // không đổi trạng thái khi bị chặn
        }
    }

    [Theory]
    [InlineData(PartnerApplicationStatus.AccountIssued)]   // đã cấp tài khoản: dừng hợp tác dùng Owners - Lock (US-096)
    [InlineData(PartnerApplicationStatus.Submitted)]       // chưa thẩm định: phải bắt đầu xem xét trước
    public void Cancel_is_not_allowed(PartnerApplicationStatus from)
        => Assert.Throws<ConflictException>(() => Application(from).MoveTo(PartnerApplicationStatus.Cancelled));

    [Theory]
    [InlineData(PartnerApplicationStatus.Activated)]
    [InlineData(PartnerApplicationStatus.Rejected)]
    [InlineData(PartnerApplicationStatus.Cancelled)]
    public void Terminal_states_have_no_next_status(PartnerApplicationStatus status)
    {
        Assert.True(Application(status).IsTerminal);
        Assert.Empty(PartnerApplication.NextStatuses(status));
    }

    [Fact]
    public void Submit_creates_submitted_application_with_code()
    {
        var app = PartnerApplication.Submit("PA-2026-0001", Now);

        Assert.Equal(PartnerApplicationStatus.Submitted, app.Status);
        Assert.Equal("PA-2026-0001", app.Code);
        Assert.Equal(Now, app.ConsentAcceptedAtUtc);
        Assert.False(app.IsTerminal);
        Assert.Equal([PartnerApplicationStatus.UnderReview], PartnerApplication.NextStatuses(PartnerApplicationStatus.Submitted));
    }

    // ==================== A1 – danh sách ====================

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task List_rejects_invalid_paging(int page, int pageSize)
    {
        var queries = new FakePartnerQueries();
        await Assert.ThrowsAsync<ValidationException>(() =>
            new ListPartnerApplicationsUseCase(queries).ExecuteAsync(null, null, page, pageSize));
        Assert.Empty(queries.ListCalls);
    }

    [Fact]
    public async Task List_rejects_too_long_search()
        => await Assert.ThrowsAsync<ValidationException>(() =>
            new ListPartnerApplicationsUseCase(new FakePartnerQueries()).ExecuteAsync(null, new string('a', 201), 1, 20));

    [Fact]
    public async Task List_trims_search_and_passes_filters()
    {
        var queries = new FakePartnerQueries();

        await new ListPartnerApplicationsUseCase(queries).ExecuteAsync(PartnerApplicationStatus.UnderReview, "  thủ đức  ", 2, 10);

        var call = Assert.Single(queries.ListCalls);
        Assert.Equal((PartnerApplicationStatus.UnderReview, "thủ đức", 2, 10), call);
    }

    [Fact]
    public async Task List_treats_blank_search_as_no_filter()
    {
        var queries = new FakePartnerQueries();
        await new ListPartnerApplicationsUseCase(queries).ExecuteAsync(null, "   ", 1, 20);
        Assert.Null(Assert.Single(queries.ListCalls).Search);
    }

    // ==================== A2 – chi tiết ====================

    [Fact]
    public async Task Get_returns_detail()
    {
        var detail = Detail(12);
        var result = await new GetPartnerApplicationUseCase(new FakePartnerQueries(detail)).ExecuteAsync(12);
        Assert.Same(detail, result);
    }

    [Fact]
    public async Task Get_unknown_id_throws_404()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => new GetPartnerApplicationUseCase(new FakePartnerQueries()).ExecuteAsync(99));
        Assert.Contains("Hồ sơ đối tác", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Get_invalid_id_throws_400(int id)
        => await Assert.ThrowsAsync<ValidationException>(() => new GetPartnerApplicationUseCase(new FakePartnerQueries()).ExecuteAsync(id));

    private static PartnerApplicationDetailDto Detail(int id) => new(
        id, "PA-2026-0012", "UnderReview", "Nguyễn Thị Lan", "lan.nguyen@thuducparking.vn", "0903456789", "Giám đốc", "Company",
        "Công ty CP Bãi xe Thủ Đức", "0316789012", "12 Võ Văn Ngân", "Nguyễn Thị Lan", "Giám đốc", null, 2, "216 Võ Văn Ngân", 640,
        1200, "06:00 – 23:00", Now, 1, null, null, null, null, null, null, Now, null,
        ["NeedsMoreInfo", "AccountIssued", "Rejected", "Cancelled"]);

    private sealed class FakePartnerQueries(params PartnerApplicationDetailDto[] details) : IPartnerApplicationQueries
    {
        public List<(PartnerApplicationStatus? Status, string? Search, int Page, int PageSize)> ListCalls { get; } = [];

        public Task<PagedResult<PartnerApplicationSummaryDto>> ListAsync(PartnerApplicationStatus? status, string? search, int page,
            int pageSize, CancellationToken cancellationToken)
        {
            ListCalls.Add((status, search, page, pageSize));
            return Task.FromResult(new PagedResult<PartnerApplicationSummaryDto>([], page, pageSize, 0));
        }

        public Task<PartnerApplicationDetailDto?> GetDetailAsync(int id, CancellationToken cancellationToken)
            => Task.FromResult(details.FirstOrDefault(d => d.Id == id));
    }
}
