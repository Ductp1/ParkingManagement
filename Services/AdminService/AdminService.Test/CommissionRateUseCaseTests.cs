using AdminService.Application.Features;
using AdminService.Application.Features.Commission;
using AdminService.Application.Features.Dashboard;
using AdminService.Application.Features.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Exceptions;
using static AdminService.Test.CommissionRateResolverTests;

namespace AdminService.Test;

// US-079: giải tỷ lệ cho PaymentService (nội bộ) và danh sách tỷ lệ cho Admin portal.
public class CommissionRateUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 3, 0, 0, TimeSpan.Zero);   // 10:00 ICT
    private static readonly DateOnly From = new(2026, 10, 1);
    private static readonly DateOnly To = new(2027, 9, 30);

    private static DateTime Utc(int month, int day, int hour, int minute = 0)
        => new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static ResolveCommissionRatesUseCase Resolver(ICommissionRateQueries queries, ISettingsQueries? settings = null,
        ListLogger<ResolveCommissionRatesUseCase>? logger = null)
        => new(queries, settings ?? new FakeCommissionSettings(), new FixedClock(Now),
            (ILogger<ResolveCommissionRatesUseCase>?)logger ?? NullLogger<ResolveCommissionRatesUseCase>.Instance);

    private static ResolveCommissionRatesRequest Items(params (int LotId, DateTime AtUtc)[] items)
        => new([.. items.Select(i => new ResolveCommissionRateItem(i.LotId, i.AtUtc))]);

    // ==================== Resolve (nội bộ) ====================

    [Fact]
    public async Task Resolve_ReturnsContractRateAndDefault_InRequestOrder()
    {
        var queries = new FakeCommissionRateQueries(Contract(lotId: 7, percent: 7.5m, from: From, to: To));

        var result = await Resolver(queries).ExecuteAsync("PaymentService", Items((9, Utc(11, 5, 3)), (7, Utc(11, 5, 3)), (7, Utc(11, 4, 1))));

        Assert.Equal([9, 7, 7], result.Items.Select(i => i.ParkingLotId));
        Assert.Equal(new ResolvedCommissionRateDto(9, Utc(11, 5, 3), 0.10m, "DefaultConfig", "NoSignedContract", null, null, null, null),
            result.Items[0]);
        Assert.Equal(new ResolvedCommissionRateDto(7, Utc(11, 5, 3), 0.075m, "LotContract", null, "HD-SP-2026-0001", 2, From, To),
            result.Items[1]);
        Assert.Equal([7, 9], queries.Requests.Single()!.Order());                       // một lần đọc, các bãi không lặp
    }

    [Fact]
    public async Task Resolve_DefaultRate_FollowsChangeHistoryAtExactInstant()
    {
        var settings = new FakeCommissionSettings("0.10",
            new SystemConfigChangePoint(1, "0.12", Utc(11, 3, 5), CancelledAtUtc: null),
            new SystemConfigChangePoint(2, "0.20", Utc(11, 4, 5), CancelledAtUtc: Utc(11, 4, 1)));   // đã hủy trước khi hiệu lực

        var result = await Resolver(new FakeCommissionRateQueries(), settings)
            .ExecuteAsync("PaymentService", Items((1, Utc(11, 3, 4, 59)), (1, Utc(11, 3, 5)), (1, Utc(11, 5, 0))));

        Assert.Equal([0.10m, 0.12m, 0.12m], result.Items.Select(i => i.Rate));
    }

    [Fact]
    public async Task Resolve_OutsideContractWindow_UsesDefault_AndWarnsOncePerLot()
    {
        var logger = new ListLogger<ResolveCommissionRatesUseCase>();
        var queries = new FakeCommissionRateQueries(Contract(lotId: 7, from: new DateOnly(2026, 11, 10), to: To));

        var result = await Resolver(queries, logger: logger).ExecuteAsync("PaymentService", Items((7, Utc(11, 5, 3)), (7, Utc(11, 5, 4))));

        Assert.All(result.Items, i =>
        {
            Assert.Equal(("DefaultConfig", "BeforeEffective", 0.10m), (i.Source, i.Reason, i.Rate));
            Assert.Equal("HD-SP-2026-0001", i.ContractNumber);
        });
        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("HD-SP-2026-0001", warning.Message);
    }

    [Theory]
    [InlineData("SupportService")]
    [InlineData("ParkingService")]
    public async Task Resolve_OtherService_Forbidden(string caller)
        => await Assert.ThrowsAsync<ForbiddenException>(() =>
            Resolver(new FakeCommissionRateQueries()).ExecuteAsync(caller, Items((1, Utc(11, 5, 3)))));

    public static TheoryData<ResolveCommissionRatesRequest?> InvalidRequests()
    {
        var future = Now.UtcDateTime.AddMinutes(6);
        return new()
        {
            null,
            new ResolveCommissionRatesRequest(null),
            new ResolveCommissionRatesRequest([]),
            new ResolveCommissionRatesRequest([.. Enumerable.Range(1, 201).Select(i => new ResolveCommissionRateItem(i, Utc(11, 5, 3)))]),
            new ResolveCommissionRatesRequest([new ResolveCommissionRateItem(0, Utc(11, 5, 3))]),
            new ResolveCommissionRatesRequest([new ResolveCommissionRateItem(null, Utc(11, 5, 3))]),
            new ResolveCommissionRatesRequest([new ResolveCommissionRateItem(1, null)]),
            new ResolveCommissionRatesRequest([new ResolveCommissionRateItem(1, future)]),
        };
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Resolve_InvalidRequest_Throws400(ResolveCommissionRatesRequest? request)
        => await Assert.ThrowsAsync<ValidationException>(() => Resolver(new FakeCommissionRateQueries()).ExecuteAsync("PaymentService", request));

    [Fact]
    public async Task Resolve_AllowsSmallClockSkew_And200Items()
    {
        var items = Enumerable.Range(1, 200).Select(i => (i, Now.UtcDateTime.AddMinutes(4))).ToArray();
        var result = await Resolver(new FakeCommissionRateQueries()).ExecuteAsync("PaymentService", Items(items));
        Assert.Equal(200, result.Items.Count);
    }

    [Fact]
    public async Task Resolve_UnspecifiedKind_IsTreatedAsUtc()
    {
        var result = await Resolver(new FakeCommissionRateQueries())
            .ExecuteAsync("PaymentService", Items((1, new DateTime(2026, 11, 5, 3, 0, 0, DateTimeKind.Unspecified))));
        Assert.Equal(DateTimeKind.Utc, result.Items[0].AtUtc.Kind);
    }

    [Fact]
    public async Task Resolve_MissingDefaultConfig_Throws503()
        => await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            Resolver(new FakeCommissionRateQueries(), new FakeCommissionSettings(defaultValue: null))
                .ExecuteAsync("PaymentService", Items((1, Utc(11, 5, 3)))));

    // ==================== Danh sách cho Admin ====================

    private static ParkingLotSource Lot(int id, string name, string? district = "Cầu Giấy", string status = "Active")
        => new(id, name, "Hà Nội", district, status, "Standard", 100, false, false);

    private static ListCommissionRatesUseCase Lister(IParkingDashboardClient parking, ISettingsQueries? settings = null,
        params SignedLotContractRate[] contracts)
        => new(new FakeCommissionRateQueries(contracts), settings ?? new FakeCommissionSettings(), parking, new FixedClock(Now),
            NullLogger<ListCommissionRatesUseCase>.Instance, new DashboardSourceTimeouts(fallback: TimeSpan.FromMilliseconds(200)));

    [Fact]
    public async Task List_UnionsParkingLotsAndSignedContracts_SkipsPendingApproval()
    {
        var parking = FakeParkingLots.Returning(Lot(1, "Vincom Bà Triệu"), Lot(2, "Bãi chờ duyệt", status: "PendingApproval"),
            Lot(7, "Bãi Xanh (tên mới)", "Ba Đình", "Suspended"));
        var contracts = new[] { Contract(id: 1, lotId: 7, from: From, to: To), Contract(id: 2, lotId: 9, percent: 6m, from: From, to: To) };

        var result = await Lister(parking, null, contracts).ExecuteAsync(new CommissionRateListQuery());

        Assert.False(result.Partial);
        Assert.Equal([7, 9, 1], result.Lots.Items.Select(r => r.ParkingLotId));            // sắp theo tên (vi-VN)
        var lot7 = result.Lots.Items[0];
        Assert.Equal(("Bãi Xanh (tên mới)", "Suspended", 30, "Công ty Bãi Xanh"), (lot7.LotName, lot7.LotStatus, lot7.OwnerProfileId, lot7.OwnerName));
        Assert.Equal((0.075m, "LotContract", (string?)null, 1, "HD-SP-2026-0001", 2),
            (lot7.CurrentRate, lot7.Source, lot7.Reason, lot7.ContractId, lot7.ContractNumber, lot7.ContractVersion));
        var lot9 = result.Lots.Items[1];                                                    // có hợp đồng, ParkingService không trả về
        Assert.Equal(("Bãi Xanh Ba Đình", "Ba Đình", (string?)null), (lot9.LotName, lot9.District, lot9.LotStatus));
        var lot1 = result.Lots.Items[2];
        Assert.Equal((0.10m, "DefaultConfig", "NoSignedContract", (int?)null, (string?)null),
            (lot1.CurrentRate, lot1.Source, lot1.Reason, lot1.OwnerProfileId, lot1.OwnerName));
    }

    [Fact]
    public async Task List_ParkingServiceError_ReturnsContractLotsOnly_AsPartial()
    {
        var result = await Lister(FakeParkingLots.Failing(), null, Contract(lotId: 7, from: From, to: To))
            .ExecuteAsync(new CommissionRateListQuery());

        Assert.True(result.Partial);
        Assert.Equal("ParkingService trả 500.", result.PartialReason);
        Assert.Equal(7, Assert.Single(result.Lots.Items).ParkingLotId);
    }

    [Fact]
    public async Task List_ParkingServiceThrows_IsPartial_Not500()
    {
        var parking = new FakeParkingLots(_ => throw new HttpRequestException("connection refused"));
        var result = await Lister(parking, null, Contract(lotId: 7, from: From, to: To)).ExecuteAsync(new CommissionRateListQuery());
        Assert.True(result.Partial);
        Assert.Single(result.Lots.Items);
    }

    [Fact]
    public async Task List_ParkingServiceTimeout_IsPartial()
    {
        var parking = new FakeParkingLots(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return SourceResult<ParkingLotsSource>.Ok(new ParkingLotsSource(DateTime.UtcNow, []));
        });
        var result = await Lister(parking).ExecuteAsync(new CommissionRateListQuery());
        Assert.True(result.Partial);
        Assert.Contains("không phản hồi kịp", result.PartialReason);
    }

    [Theory]
    [InlineData("vincom", 1)]
    [InlineData("BA DINH", 7)]                       // không phân biệt hoa thường / dấu, "đ" khớp "d"
    [InlineData("bãi xanh", 7)]
    [InlineData("HD-SP-2026-0001", 7)]
    [InlineData("#3", 3)]
    public async Task List_Search_MatchesLotDistrictOwnerContractOrId(string search, int expectedLotId)
    {
        var parking = FakeParkingLots.Returning(Lot(1, "Vincom Bà Triệu", "Hai Bà Trưng"), Lot(3, "Bãi Lotte", "Đống Đa"),
            Lot(7, "Bãi số 7", "Ba Đình"));
        var result = await Lister(parking, null, Contract(lotId: 7, from: From, to: To))
            .ExecuteAsync(new CommissionRateListQuery(Search: search));
        Assert.Equal(expectedLotId, Assert.Single(result.Lots.Items).ParkingLotId);
    }

    [Fact]
    public async Task List_SourceFilter_And_Paging()
    {
        var parking = FakeParkingLots.Returning(Lot(1, "A"), Lot(2, "B"), Lot(3, "C"), Lot(7, "D"));
        var lister = Lister(parking, null, Contract(lotId: 7, from: From, to: To));

        var contractsOnly = await lister.ExecuteAsync(new CommissionRateListQuery(Source: "lotcontract"));
        var page2 = await lister.ExecuteAsync(new CommissionRateListQuery(Source: "DefaultConfig", Page: 2, PageSize: 2));

        Assert.Equal(7, Assert.Single(contractsOnly.Lots.Items).ParkingLotId);
        Assert.Equal((2, 2, 3), (page2.Lots.Page, page2.Lots.PageSize, page2.Lots.TotalCount));
        Assert.Equal(3, Assert.Single(page2.Lots.Items).ParkingLotId);
    }

    [Theory]
    [InlineData(0, 20, null)]
    [InlineData(1, 0, null)]
    [InlineData(1, 101, null)]
    [InlineData(1, 20, "Contract")]
    public async Task List_InvalidQuery_Throws400(int page, int pageSize, string? source)
        => await Assert.ThrowsAsync<ValidationException>(() =>
            Lister(FakeParkingLots.Returning()).ExecuteAsync(new CommissionRateListQuery(null, source, page, pageSize)));

    [Fact]
    public async Task List_ReportsCurrentDefault_AndEarliestScheduledChange()
    {
        var settings = new FakeCommissionSettings("0.10",
            new SystemConfigChangePoint(1, "0.11", Utc(10, 1, 0), null),
            new SystemConfigChangePoint(2, "0.13", Utc(12, 1, 0), null),
            new SystemConfigChangePoint(3, "0.12", Utc(11, 20, 0), null),
            new SystemConfigChangePoint(4, "0.09", Utc(11, 10, 0), CancelledAtUtc: Utc(11, 2, 0)));

        var result = await Lister(FakeParkingLots.Returning(Lot(1, "A")), settings).ExecuteAsync(new CommissionRateListQuery());

        Assert.Equal(0.11m, result.DefaultRate);
        Assert.Equal(Utc(10, 1, 0), result.DefaultEffectiveFromUtc);
        Assert.Equal(new CommissionDefaultChangeDto(0.12m, Utc(11, 20, 0)), result.DefaultNextChange);
        Assert.Equal(0.11m, result.Lots.Items[0].CurrentRate);
    }

    [Fact]
    public async Task List_SignedContractNotYetEffective_ShowsDefaultWithReasonAndContract()
    {
        var result = await Lister(FakeParkingLots.Returning(Lot(7, "Bãi 7")), null,
                Contract(lotId: 7, from: new DateOnly(2026, 12, 1), to: To))
            .ExecuteAsync(new CommissionRateListQuery());

        var row = Assert.Single(result.Lots.Items);
        Assert.Equal(("DefaultConfig", "BeforeEffective", "HD-SP-2026-0001"), (row.Source, row.Reason, row.ContractNumber));
        Assert.Equal(new DateOnly(2026, 12, 1), row.EffectiveFrom);
    }
}
