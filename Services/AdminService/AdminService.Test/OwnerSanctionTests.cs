using AdminService.Application.Features.Owners;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-096: lịch sử vi phạm của chủ bãi (unit test với fake ISanctionQueries, không cần database).
public class OwnerSanctionTests
{
    private const int OwnerVincom = 1;
    private const int OwnerTsn = 2;

    private static readonly DateTime Now = new(2026, 10, 9, 8, 0, 0, DateTimeKind.Utc);

    private static readonly SanctionDto TsnLotSuspension =
        new(1, OwnerTsn, 4, "TemporarySuspension", "Để xảy ra overbooking 3 lần trong tuần.", Now.AddDays(-7), Now.AddDays(7), "Active", "NotRequired", null, null, 1, Now.AddDays(-7), null);
    private static readonly SanctionDto TsnOwnerWarning =
        new(2, OwnerTsn, null, "Warning", "Phản hồi khiếu nại trễ hạn.", Now.AddDays(-30), null, "Expired", "NotRequired", null, null, 1, Now.AddDays(-30), Now.AddDays(-20));
    private static readonly SanctionDto VincomWarning =
        new(3, OwnerVincom, null, "Warning", "Cập nhật giá chậm.", Now.AddDays(-3), null, "Active", "NotRequired", null, null, 1, Now.AddDays(-3), null);

    [Fact]
    public async Task Get_owner_sanctions_returns_only_that_owner_newest_first()
    {
        var queries = new FakeSanctionQueries(TsnOwnerWarning, VincomWarning, TsnLotSuspension);

        var result = await new GetOwnerSanctionsUseCase(queries).ExecuteAsync(OwnerTsn, 1, 20);

        Assert.Equal(new[] { TsnLotSuspension.Id, TsnOwnerWarning.Id }, result.Items.Select(s => s.Id).ToArray());
        Assert.All(result.Items, s => Assert.Equal(OwnerTsn, s.OwnerProfileId));
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task Get_owner_sanctions_passes_paging_to_queries()
    {
        var queries = new FakeSanctionQueries(TsnOwnerWarning, TsnLotSuspension);

        var result = await new GetOwnerSanctionsUseCase(queries).ExecuteAsync(OwnerTsn, 2, 1);

        var call = Assert.Single(queries.Calls);
        Assert.Equal(OwnerTsn, call.OwnerProfileId);
        Assert.Equal(2, call.Page);
        Assert.Equal(1, call.PageSize);
        Assert.Equal(TsnOwnerWarning.Id, Assert.Single(result.Items).Id);   // trang 2 = chế tài cũ hơn
        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task Get_sanctions_of_owner_without_violation_returns_empty_page()
    {
        var result = await new GetOwnerSanctionsUseCase(new FakeSanctionQueries(VincomWarning)).ExecuteAsync(999, 1, 20);

        Assert.Empty(result.Items);                         // không ném NotFound: chưa có chế tài nào
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task Get_owner_sanctions_accepts_page_size_boundaries(int pageSize)
    {
        var queries = new FakeSanctionQueries(VincomWarning);

        await new GetOwnerSanctionsUseCase(queries).ExecuteAsync(OwnerVincom, 1, pageSize);

        Assert.Single(queries.Calls);
    }

    [Theory]
    [InlineData(0, 1, 20, "ownerProfileId phải là số nguyên dương.")]
    [InlineData(-3, 1, 20, "ownerProfileId phải là số nguyên dương.")]
    [InlineData(2, 0, 20, "page phải ≥ 1.")]
    [InlineData(2, -1, 20, "page phải ≥ 1.")]
    [InlineData(2, 1, 0, "pageSize phải trong khoảng 1–100.")]
    [InlineData(2, 1, 101, "pageSize phải trong khoảng 1–100.")]
    public async Task Get_owner_sanctions_with_invalid_input_is_rejected(int ownerProfileId, int page, int pageSize, string expectedMessage)
    {
        var queries = new FakeSanctionQueries(TsnLotSuspension);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => new GetOwnerSanctionsUseCase(queries).ExecuteAsync(ownerProfileId, page, pageSize));

        Assert.Equal(expectedMessage, ex.Message);
        Assert.Empty(queries.Calls);                        // không truy vấn khi đầu vào sai
    }

    private sealed class FakeSanctionQueries(params SanctionDto[] sanctions) : ISanctionQueries
    {
        public List<(int OwnerProfileId, int Page, int PageSize)> Calls { get; } = [];

        public Task<PagedResult<SanctionDto>> ListByOwnerAsync(int ownerProfileId, int page, int pageSize, CancellationToken cancellationToken)
        {
            Calls.Add((ownerProfileId, page, pageSize));

            var matched = sanctions.Where(s => s.OwnerProfileId == ownerProfileId)
                .OrderByDescending(s => s.StartsAtUtc).ThenByDescending(s => s.Id).ToList();
            var items = matched.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedResult<SanctionDto>(items, page, pageSize, matched.Count));
        }
    }
}
