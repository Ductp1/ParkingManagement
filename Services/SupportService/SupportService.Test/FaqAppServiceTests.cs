using ParkingManagement.SharedKernel.Exceptions;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;
using SupportService.Application.Services;

namespace SupportService.Test;

public class FaqAppServiceTests
{
    private static readonly FaqItemDto[] Seed =
    [
        new(1, "Booking", "Driver", "Giữ chỗ bao lâu?", "15 phút.", 1, 0),
        new(2, "Refund", "Driver", "Hủy có được hoàn?", "Trước 60 phút hoàn 100%.", 2, 0),
        new(3, "Booking", "All", "Đến trễ thì sao?", "Ân hạn 15 phút.", 3, 0),
        new(4, "ParkingLotOwner", "LotOwner", "Bao lâu nhận tiền?", "Quyết toán hằng tuần.", 4, 0),
    ];

    private static FaqAppService CreateService(out FakeFaqQueries queries)
    {
        queries = new FakeFaqQueries(Seed);
        var settings = new HelpCenterSettings { Contacts = [new("Hotline", "1900 0000")] };
        return new FaqAppService(queries, settings);
    }

    [Fact]
    public async Task Driver_sees_driver_and_common_faqs_but_not_owner_faqs()
    {
        var result = await CreateService(out _).GetHelpCenterAsync("driver", null);

        var ids = result.Categories.SelectMany(c => c.Items).Select(f => f.Id).ToList();
        Assert.Equal("Driver", result.Audience);
        Assert.Equal([1, 3, 2], ids);
        Assert.DoesNotContain(4, ids);
    }

    [Fact]
    public async Task Faqs_are_grouped_by_category_in_sort_order_with_contacts()
    {
        var result = await CreateService(out _).GetHelpCenterAsync("Driver", null);

        Assert.Equal(["Booking", "Refund"], result.Categories.Select(c => c.Category));
        Assert.Equal([1, 3], result.Categories[0].Items.Select(f => f.Id));
        Assert.Single(result.Contacts);
    }

    [Fact]
    public async Task Missing_audience_only_returns_common_faqs()
    {
        var result = await CreateService(out _).GetHelpCenterAsync(null, null);

        Assert.Equal("All", result.Audience);
        Assert.All(result.Categories.SelectMany(c => c.Items), f => Assert.Equal("All", f.Audience));
    }

    [Fact]
    public async Task Category_filter_is_passed_to_queries()
    {
        var result = await CreateService(out _).GetHelpCenterAsync("Driver", " Refund ");

        Assert.Equal([2], result.Categories.SelectMany(c => c.Items).Select(f => f.Id));
    }

    [Fact]
    public async Task Unknown_audience_is_rejected()
        => await Assert.ThrowsAsync<ValidationException>(() => CreateService(out _).GetHelpCenterAsync("Admin", null));

    [Fact]
    public async Task Get_by_id_counts_a_view()
    {
        var service = CreateService(out var queries);

        var faq = await service.GetByIdAsync(1);

        Assert.Equal(1, faq.ViewCount);
        Assert.Equal(1, queries.Views[1]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Non_positive_id_is_rejected(int id)
        => await Assert.ThrowsAsync<ValidationException>(() => CreateService(out _).GetByIdAsync(id));

    [Fact]
    public async Task Unknown_id_returns_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => CreateService(out _).GetByIdAsync(99));

    private sealed class FakeFaqQueries(IEnumerable<FaqItemDto> faqs) : IFaqQueries
    {
        private readonly List<FaqItemDto> _faqs = faqs.ToList();
        public Dictionary<int, int> Views { get; } = [];

        public Task<IReadOnlyList<FaqItemDto>> ListPublishedAsync(IReadOnlyCollection<string> audiences, string? category, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FaqItemDto>>(_faqs
                .Where(f => audiences.Contains(f.Audience) && (category is null || f.Category == category))
                .OrderBy(f => f.Category).ThenBy(f => f.SortOrder)
                .ToList());

        public Task<FaqItemDto?> GetPublishedAndCountViewAsync(int id, CancellationToken cancellationToken = default)
        {
            var faq = _faqs.FirstOrDefault(f => f.Id == id);
            if (faq is null) return Task.FromResult<FaqItemDto?>(null);
            Views[id] = Views.GetValueOrDefault(id) + 1;
            return Task.FromResult<FaqItemDto?>(faq with { ViewCount = faq.ViewCount + Views[id] });
        }
    }
}
