using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using AdminService.API.Controllers;
using AdminService.Application;
using AdminService.Application.Features.Dashboard;
using AdminService.Application.Features.LotContracts;
using AdminService.Infrastructure;
using AdminService.Infrastructure.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-081: Admin Dashboard overview (fake IDashboardQueries + fake IParkingServiceClient, không cần database hay ParkingService).
public class DashboardOverviewTests
{
    // 23:30 ICT ngày 27/10/2026.
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 16, 30, 0, TimeSpan.Zero);
    private static readonly DateTime NowUtc = Now.UtcDateTime;

    private static GetDashboardOverviewUseCase CreateUseCase(FakeQueries queries, FakeParking parking, DateTimeOffset? now = null, TimeSpan? timeout = null)
        => new(queries, parking, new FixedTimeProvider(now ?? Now), NullLogger<GetDashboardOverviewUseCase>.Instance, timeout);

    // ==================== Số liệu đúng ====================

    [Fact]
    public async Task Internal_widgets_carry_counts_from_admin_database()
    {
        var queries = new FakeQueries();

        var overview = await CreateUseCase(queries, new FakeParking()).ExecuteAsync();

        var partners = Available(overview.Queues.PartnerApplications, "AdminService");
        Assert.Equal(new PartnerQueueDto(3, 2, 1, 4), partners);
        Assert.Equal(5, partners.ToReview);
        Assert.Equal(new LotContractsQueueDto(6), Available(overview.Queues.LotContractsToSign, "AdminService"));
        var risk = Available(overview.Queues.RiskFlags, "AdminService");
        Assert.Equal(new RiskFlagsDto(4, 1, 1, 2, 2, 3), risk);
        Assert.Equal(5, risk.Unresolved);
        Assert.Equal(new ActiveSanctionsDto(3, 1, 0, 1, 1), Available(overview.Governance.ActiveSanctions, "AdminService"));
        Assert.Equal(new ScheduledConfigChangesDto(2, "HOLD_TTL_MINUTES", NowUtc.AddDays(1)), Available(overview.Governance.ScheduledConfigChanges, "AdminService"));
        var audit = Available(overview.Governance.AuditLogsToday, "AdminService");
        Assert.Equal(9, audit.Total);
        Assert.Equal(["AdminService", "ParkingService"], audit.BySource.Select(s => s.SourceService));
        Assert.Equal(NowUtc, overview.Governance.ActiveSanctions.AsOfUtc);
        Assert.Equal(NowUtc, overview.GeneratedAtUtc);
        Assert.Equal("Asia/Ho_Chi_Minh", overview.TimeZone);
    }

    [Fact]
    public async Task Kyb_queue_counts_each_status_from_parking_service()
    {
        var parking = new FakeParking { Counts = { ["Submitted"] = 14, ["UnderReview"] = 2, ["NeedsMoreInfo"] = 5 } };

        var overview = await CreateUseCase(new FakeQueries(), parking).ExecuteAsync();

        var kyb = Available(overview.Queues.KybApplications, "ParkingService");
        Assert.Equal(new KybQueueDto(14, 2, 5), kyb);
        Assert.Equal(16, kyb.ToReview);
        Assert.Equal(["NeedsMoreInfo", "Submitted", "UnderReview"], parking.Requested.Order());
    }

    [Fact]
    public async Task Moment_queries_receive_current_time()
    {
        var queries = new FakeQueries();

        await CreateUseCase(queries, new FakeParking()).ExecuteAsync();

        Assert.Equal([NowUtc, NowUtc, NowUtc], queries.NowArguments);
    }

    // ==================== "0 thật" khác "không có dữ liệu" ====================

    [Fact]
    public async Task Real_zero_is_available_with_zero_values()
    {
        var queries = new FakeQueries
        {
            Partners = new PartnerQueueDto(0, 0, 0, 0),
            RiskFlags = new RiskFlagsDto(0, 0, 0, 0, 0, 0),
            Audit = new AuditTodayDto(0, []),
        };
        var parking = new FakeParking { Counts = { ["Submitted"] = 0, ["UnderReview"] = 0, ["NeedsMoreInfo"] = 0 } };

        var overview = await CreateUseCase(queries, parking).ExecuteAsync();

        Assert.Equal(0, Available(overview.Queues.PartnerApplications, "AdminService").ToReview);
        Assert.Equal(0, Available(overview.Queues.KybApplications, "ParkingService").ToReview);
        Assert.Equal(0, Available(overview.Governance.AuditLogsToday, "AdminService").Total);
        Assert.Equal(DashboardWidgetStatus.Available, overview.Queues.RiskFlags.Status);
    }

    [Fact]
    public async Task Widgets_without_source_are_unavailable_never_zero()
    {
        var overview = await CreateUseCase(new FakeQueries(), new FakeParking()).ExecuteAsync();

        var noSource = new (DashboardWidget<DashboardNoValue> Widget, string Source)[]
        {
            (overview.Kpis.RevenueToday, "PaymentService"), (overview.Kpis.ActiveSessions, "GateService"),
            (overview.Kpis.BookingsToday, "BookingService"), (overview.Kpis.OpenDisputes, "SupportService"),
            (overview.Queues.RefundsRequested, "PaymentService"), (overview.Queues.SettlementsPendingReview, "PaymentService"),
            (overview.Queues.DisputesAwaitingOwner, "SupportService"), (overview.Queues.DataSubjectRequestsDue, "UserService"),
            (overview.Tables.LotOccupancy, "ParkingService"), (overview.Tables.RecentPayments, "PaymentService"),
            (overview.Tables.GateDevices, "GateService"),
        };
        foreach (var (widget, source) in noSource)
        {
            Assert.Equal(DashboardWidgetStatus.Unavailable, widget.Status);
            Assert.Equal(DashboardWidgetStatus.NoSource, widget.Reason);
            Assert.Equal(source, widget.Source);
            Assert.Null(widget.Value);
            Assert.Null(widget.AsOfUtc);
            Assert.Contains("chưa có", widget.Detail);
        }
    }

    [Fact]
    public async Task Json_uses_lowercase_status_and_reason_and_null_value_for_unavailable()
    {
        var overview = await CreateUseCase(new FakeQueries(), new FakeParking()).ExecuteAsync();

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(overview, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var revenue = json.RootElement.GetProperty("kpis").GetProperty("revenueToday");
        Assert.Equal("unavailable", revenue.GetProperty("status").GetString());
        Assert.Equal("no_source", revenue.GetProperty("reason").GetString());
        Assert.Equal(JsonValueKind.Null, revenue.GetProperty("value").ValueKind);
        var partners = json.RootElement.GetProperty("queues").GetProperty("partnerApplications");
        Assert.Equal("available", partners.GetProperty("status").GetString());
        Assert.Equal(5, partners.GetProperty("value").GetProperty("toReview").GetInt32());
    }

    // ==================== Ngày theo giờ Việt Nam ====================

    [Theory]
    [InlineData("2026-10-27T16:59:59Z", "2026-10-26T17:00:00Z")]   // 23:59:59 ICT ngày 27
    [InlineData("2026-10-27T17:00:00Z", "2026-10-27T17:00:00Z")]   // 00:00 ICT ngày 28
    [InlineData("2026-10-27T00:00:00Z", "2026-10-26T17:00:00Z")]   // 07:00 ICT ngày 27
    [InlineData("2026-12-31T17:30:00Z", "2026-12-31T17:00:00Z")]   // 00:30 ICT 01/01/2027
    public async Task Today_is_the_ict_calendar_day(string now, string expectedFrom)
    {
        var queries = new FakeQueries();
        var nowUtc = DateTimeOffset.Parse(now);

        var overview = await CreateUseCase(queries, new FakeParking(), nowUtc).ExecuteAsync();

        var from = DateTimeOffset.Parse(expectedFrom).UtcDateTime;
        Assert.Equal(from, overview.TodayFromUtc);
        Assert.Equal(from.AddDays(1), overview.TodayToUtc);
        Assert.Equal(DateTimeKind.Utc, overview.TodayFromUtc.Kind);
        Assert.Equal((from, from.AddDays(1)), queries.AuditRange);
    }

    // ==================== Nguồn lỗi không làm hỏng response ====================

    public static TheoryData<Exception> SourceFailures => new()
    {
        new DependencyUnavailableException("ParkingService hiện không phản hồi."),
        new AuthenticationException("Phiên đăng nhập không hợp lệ."),
        new ForbiddenException("Không có quyền."),
        new ConflictException("Từ chối."),
    };

    [Theory]
    [MemberData(nameof(SourceFailures))]
    public async Task Parking_service_failure_only_marks_kyb_widget_as_source_error(Exception failure)
    {
        var overview = await CreateUseCase(new FakeQueries(), new FakeParking { Failure = failure }).ExecuteAsync();

        var kyb = overview.Queues.KybApplications;
        Assert.Equal(DashboardWidgetStatus.Unavailable, kyb.Status);
        Assert.Equal(DashboardWidgetStatus.SourceError, kyb.Reason);
        Assert.Equal("ParkingService", kyb.Source);
        Assert.Null(kyb.Value);
        Assert.Contains("KYB", kyb.Detail);
        Assert.Equal(DashboardWidgetStatus.Available, overview.Queues.PartnerApplications.Status);   // phần còn lại vẫn có số
        Assert.Equal(DashboardWidgetStatus.Available, overview.Governance.AuditLogsToday.Status);
    }

    [Fact]
    public async Task One_failing_status_count_makes_whole_kyb_widget_source_error()
    {
        var parking = new FakeParking { Counts = { ["Submitted"] = 3, ["UnderReview"] = 1 }, FailStatus = "NeedsMoreInfo" };

        var overview = await CreateUseCase(new FakeQueries(), parking).ExecuteAsync();

        Assert.Equal(DashboardWidgetStatus.SourceError, overview.Queues.KybApplications.Reason);
        Assert.Null(overview.Queues.KybApplications.Value);                                       // không trả số thiếu
    }

    [Fact]
    public async Task Slow_parking_service_times_out_without_failing_the_dashboard()
    {
        var parking = new FakeParking { Hang = true };

        var overview = await CreateUseCase(new FakeQueries(), parking, timeout: TimeSpan.FromMilliseconds(80)).ExecuteAsync();

        Assert.Equal(DashboardWidgetStatus.SourceError, overview.Queues.KybApplications.Reason);
        Assert.Contains("không phản hồi kịp", overview.Queues.KybApplications.Detail);
        Assert.True(parking.ObservedCancellation);
    }

    [Fact]
    public async Task Parking_calls_run_in_parallel()
    {
        // Không đo thời gian thật (CI chậm, có coverage): mỗi lời gọi chờ tới khi đủ 3 lời gọi cùng đang chạy.
        // Nếu use case gọi nối tiếp, lời gọi đầu chờ mãi → hết timeout → widget source_error và test fail.
        var parking = new FakeParking { WaitForAll = 3, Counts = { ["Submitted"] = 1, ["UnderReview"] = 1, ["NeedsMoreInfo"] = 1 } };

        var overview = await CreateUseCase(new FakeQueries(), parking, timeout: TimeSpan.FromSeconds(10)).ExecuteAsync();

        Assert.Equal(DashboardWidgetStatus.Available, overview.Queues.KybApplications.Status);
        Assert.Equal(3, parking.MaxConcurrent);
    }

    [Fact]
    public async Task Failing_admin_query_only_marks_its_widget()
    {
        var queries = new FakeQueries { FailAudit = true };

        var overview = await CreateUseCase(queries, new FakeParking()).ExecuteAsync();

        Assert.Equal(DashboardWidgetStatus.SourceError, overview.Governance.AuditLogsToday.Reason);
        Assert.Equal("AdminService", overview.Governance.AuditLogsToday.Source);
        Assert.Equal(DashboardWidgetStatus.Available, overview.Governance.ActiveSanctions.Status);
        Assert.Equal(DashboardWidgetStatus.Available, overview.Queues.RiskFlags.Status);
    }

    [Fact]
    public async Task Cancelled_request_is_not_reported_as_source_error()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateUseCase(new FakeQueries { HonourCancellation = true }, new FakeParking { Hang = true }).ExecuteAsync(cts.Token));
    }

    // ==================== Quyền + đăng ký ====================

    [Fact]
    public void Endpoint_requires_admin_policy()
    {
        var authorize = typeof(AdminDashboardController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal("RequireAdmin", authorize?.Policy);
        Assert.Null(typeof(AdminDashboardController).GetCustomAttribute<AllowAnonymousAttribute>());
        var route = typeof(AdminDashboardController).GetCustomAttribute<Microsoft.AspNetCore.Mvc.RouteAttribute>();
        Assert.Equal("api/v1/admin/dashboard", route?.Template);
    }

    [Fact]
    public void Use_case_and_queries_are_registered()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ServiceDb"] = "Host=dashboard.invalid;Database=pm_admin_test",
            ["Services:GatewayBaseUrl"] = "http://localhost:5000",
        }).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton(TimeProvider.System);
        services.AddAdminApplication();
        services.AddAdminInfrastructure(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.IsType<GetDashboardOverviewUseCase>(scope.ServiceProvider.GetRequiredService<IGetDashboardOverviewUseCase>());
    }

    // ==================== Adapter ParkingService ====================

    [Fact]
    public async Task Parking_client_counts_kyb_from_paged_total()
    {
        Uri? requested = null;
        var handler = new StubHandler(r =>
        {
            requested = r.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "items": [ { "id": 1 } ], "page": 1, "pageSize": 1, "totalCount": 14, "totalPages": 14 }""",
                    Encoding.UTF8, "application/json"),
            };
        });
        var client = new ParkingServiceClient(new HttpClient(handler) { BaseAddress = new Uri("http://gateway.test/") }, NullLogger<ParkingServiceClient>.Instance);

        Assert.Equal(14, await client.CountKybApplicationsAsync("Submitted", CancellationToken.None));
        Assert.Equal("http://gateway.test/api/v1/parking-lots/kyb-applications?status=Submitted&page=1&pageSize=1", requested!.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, typeof(AuthenticationException))]
    [InlineData(HttpStatusCode.Forbidden, typeof(ForbiddenException))]
    [InlineData(HttpStatusCode.InternalServerError, typeof(DependencyUnavailableException))]
    [InlineData(HttpStatusCode.NotFound, typeof(DependencyUnavailableException))]
    public async Task Parking_client_count_failures_map_to_exceptions(HttpStatusCode status, Type expected)
    {
        var client = new ParkingServiceClient(new HttpClient(new StubHandler(_ => new HttpResponseMessage(status))) { BaseAddress = new Uri("http://gateway.test/") },
            NullLogger<ParkingServiceClient>.Instance);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => client.CountKybApplicationsAsync("Submitted", CancellationToken.None));
        Assert.IsType(expected, error);
    }

    // ==================== Helpers & fakes ====================

    private static T Available<T>(DashboardWidget<T> widget, string source) where T : class
    {
        Assert.Equal(DashboardWidgetStatus.Available, widget.Status);
        Assert.Null(widget.Reason);
        Assert.Equal(source, widget.Source);
        Assert.NotNull(widget.AsOfUtc);
        return Assert.IsType<T>(widget.Value);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeQueries : IDashboardQueries
    {
        public PartnerQueueDto Partners { get; init; } = new(3, 2, 1, 4);
        public RiskFlagsDto RiskFlags { get; init; } = new(4, 1, 1, 2, 2, 3);
        public AuditTodayDto Audit { get; init; } = new(9, [new("AdminService", 6), new("ParkingService", 3)]);
        public bool FailAudit { get; init; }
        public bool HonourCancellation { get; init; }
        public List<DateTime> NowArguments { get; } = [];
        public (DateTime From, DateTime To)? AuditRange { get; private set; }

        private Task<T> Result<T>(T value, CancellationToken cancellationToken)
        {
            if (HonourCancellation) cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(value);
        }

        public Task<PartnerQueueDto> CountPartnerApplicationsAsync(CancellationToken cancellationToken) => Result(Partners, cancellationToken);
        public Task<LotContractsQueueDto> CountLotContractsToSignAsync(CancellationToken cancellationToken) => Result(new LotContractsQueueDto(6), cancellationToken);

        public Task<RiskFlagsDto> CountRiskFlagsAsync(DateTime nowUtc, CancellationToken cancellationToken)
        {
            NowArguments.Add(nowUtc);
            return Result(RiskFlags, cancellationToken);
        }

        public Task<ActiveSanctionsDto> CountActiveSanctionsAsync(DateTime nowUtc, CancellationToken cancellationToken)
        {
            NowArguments.Add(nowUtc);
            return Result(new ActiveSanctionsDto(3, 1, 0, 1, 1), cancellationToken);
        }

        public Task<ScheduledConfigChangesDto> GetScheduledConfigChangesAsync(DateTime nowUtc, CancellationToken cancellationToken)
        {
            NowArguments.Add(nowUtc);
            return Result(new ScheduledConfigChangesDto(2, "HOLD_TTL_MINUTES", nowUtc.AddDays(1)), cancellationToken);
        }

        public Task<AuditTodayDto> CountAuditLogsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        {
            AuditRange = (fromUtc, toUtc);
            if (FailAudit) throw new InvalidOperationException("mất kết nối pm_admin");
            return Result(Audit, cancellationToken);
        }
    }

    private sealed class FakeParking : IParkingServiceClient
    {
        private int running;
        public Dictionary<string, int> Counts { get; } = new() { ["Submitted"] = 1, ["UnderReview"] = 1, ["NeedsMoreInfo"] = 1 };
        public Exception? Failure { get; init; }
        public string? FailStatus { get; init; }
        public bool Hang { get; init; }
        /// <summary>Số lời gọi phải cùng đang chạy trước khi bất kỳ lời gọi nào trả kết quả (kiểm tra song song).</summary>
        public int WaitForAll { get; init; }
        private readonly TaskCompletionSource allStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Requested { get; } = [];
        public bool ObservedCancellation { get; private set; }
        public int MaxConcurrent { get; private set; }

        public async Task<int> CountKybApplicationsAsync(string status, CancellationToken cancellationToken)
        {
            lock (Requested) Requested.Add(status);
            var now = Interlocked.Increment(ref running);
            lock (Requested) MaxConcurrent = Math.Max(MaxConcurrent, now);
            try
            {
                if (Failure is not null) throw Failure;
                if (status == FailStatus) throw new DependencyUnavailableException("ParkingService hiện không phản hồi.");
                if (Hang)
                {
                    try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                    catch (OperationCanceledException) { ObservedCancellation = true; throw; }
                }
                if (WaitForAll > 0)
                {
                    if (now >= WaitForAll) allStarted.TrySetResult();
                    await allStarted.Task.WaitAsync(cancellationToken);
                }
                return Counts[status];
            }
            finally
            {
                Interlocked.Decrement(ref running);
            }
        }

        public Task<KybLotInfo?> GetKybAsync(int kybApplicationId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ApproveKybAsync(int kybApplicationId, string contractNumber, string? note, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
