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

// US-081: Admin Dashboard overview (fake IDashboardQueries + fake client từng service nguồn, không cần database hay service khác).
public class DashboardOverviewTests
{
    // 23:30 ICT ngày 27/10/2026.
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 16, 30, 0, TimeSpan.Zero);
    private static readonly DateTime NowUtc = Now.UtcDateTime;
    private static readonly DateTime SourceAsOf = NowUtc.AddSeconds(-5);

    private static GetDashboardOverviewUseCase CreateUseCase(FakeQueries queries, FakeParking parking, FakeSources? sources = null,
        DateTimeOffset? now = null, DashboardSourceTimeouts? timeouts = null)
    {
        sources ??= new FakeSources();
        return new(queries, parking, sources, sources, sources, sources, sources, sources, new FixedTimeProvider(now ?? Now),
            NullLogger<GetDashboardOverviewUseCase>.Instance, timeouts);
    }

    private static DashboardSourceTimeouts Timeouts(string service, TimeSpan timeout, TimeSpan? fallback = null)
        => new(new Dictionary<string, TimeSpan> { [service] = timeout }, fallback);

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

    [Fact]
    public async Task Gate_widgets_carry_active_sessions_and_device_matrix()
    {
        var overview = await CreateUseCase(new FakeQueries(), new FakeParking()).ExecuteAsync();

        Assert.Equal(new ActiveSessionsDto(9, 3, 6, 1), Available(overview.Kpis.ActiveSessions, "GateService"));
        Assert.Equal(SourceAsOf, overview.Kpis.ActiveSessions.AsOfUtc);                     // thời điểm của nguồn
        var devices = Available(overview.Tables.GateDevices, "GateService");
        Assert.Equal((2, 1, 1, 4), (devices.Online, devices.Offline, devices.Maintenance, devices.Total));
        Assert.Equal([new GateDeviceTypeRowDto("AnprCamera", 0, 0, 1), new GateDeviceTypeRowDto("QrScanner", 2, 1, 0)], devices.ByType);
    }

    [Fact]
    public async Task Support_widgets_split_open_disputes_and_overdue_owner_replies()
    {
        var overview = await CreateUseCase(new FakeQueries(), new FakeParking()).ExecuteAsync();

        var open = Available(overview.Kpis.OpenDisputes, "SupportService");
        Assert.Equal(new OpenDisputesDto(2, 3, 1, 1), open);
        Assert.Equal(7, open.Unresolved);
        Assert.Equal(new DisputesAwaitingOwnerDto(1, 3), Available(overview.Queues.DisputesAwaitingOwner, "SupportService"));
    }

    [Fact]
    public async Task Lot_occupancy_merges_gate_sessions_with_parking_lots_and_groups_by_district()
    {
        var overview = await CreateUseCase(new FakeQueries(), new FakeParking()).ExecuteAsync();

        var occupancy = Available(overview.Tables.LotOccupancy, "GateService + ParkingService");
        // Bãi 2 chưa cấu hình sức chứa ở GateService → dùng TotalSlots; bãi 4 đang chờ duyệt KYB → không tính; bãi 99 không có ở ParkingService → bỏ.
        Assert.Equal(
        [
            new LotOccupancyRowDto(2, "Bãi TSN", "TP.HCM", "Tân Bình", "Active", "Manual", 4, 10, "parking", 40.0, true, false),
            new LotOccupancyRowDto(1, "Bãi Vincom", "TP.HCM", "Quận 1", "Active", "PmsApi", 5, 100, "gate", 5.0, false, false),
            new LotOccupancyRowDto(3, "Bãi Landmark", "TP.HCM", "Bình Thạnh", "Suspended", "Manual", 0, 40, "gate", 0.0, false, true),
        ], occupancy.Lots);
        Assert.Equal((9, 150, 6.0), (occupancy.TotalActive, occupancy.TotalCapacity, occupancy.OccupancyPercent));
        Assert.Equal(
        [
            new DistrictOccupancyRowDto("TP.HCM", "Tân Bình", 1, 4, 10, 40.0),
            new DistrictOccupancyRowDto("TP.HCM", "Quận 1", 1, 5, 100, 5.0),
            new DistrictOccupancyRowDto("TP.HCM", "Bình Thạnh", 1, 0, 40, 0.0),
        ], occupancy.ByDistrict);
        Assert.Equal(SourceAsOf.AddSeconds(-10), overview.Tables.LotOccupancy.AsOfUtc);   // thời điểm cũ hơn trong 2 nguồn
    }

    [Fact]
    public async Task Lots_in_same_district_are_summed()
    {
        var sources = new FakeSources
        {
            Lots = SourceResult<ParkingLotsSource>.Ok(new ParkingLotsSource(SourceAsOf,
            [
                new(1, "A", "TP.HCM", "Quận 1", "Active", "Manual", 100, false, false),
                new(2, "B", "TP.HCM", "Quận 1", "Active", "Manual", 10, false, false),
            ])),
        };

        var overview = await CreateUseCase(new FakeQueries(), new FakeParking(), sources).ExecuteAsync();

        var district = Assert.Single(Available(overview.Tables.LotOccupancy, "GateService + ParkingService").ByDistrict);
        Assert.Equal(new DistrictOccupancyRowDto("TP.HCM", "Quận 1", 2, 9, 110, 8.2), district);
    }

    [Fact]
    public async Task Payment_summary_feeds_revenue_refunds_and_methods_for_the_ict_day()
    {
        var sources = new FakeSources();

        var overview = await CreateUseCase(new FakeQueries(), new FakeParking(), sources).ExecuteAsync();

        Assert.Equal(new RevenueTodayDto(12, 1_500_000m, 100_000m, 1_400_000m), Available(overview.Kpis.RevenueToday, "PaymentService"));
        Assert.Equal(new RefundsRequestedDto(2), Available(overview.Queues.RefundsRequested, "PaymentService"));
        Assert.Equal([new PaymentMethodRowDto("Vnpay", 5, 900_000m), new PaymentMethodRowDto("Cash", 7, 600_000m)],
            Available(overview.Tables.PaymentsByMethodToday, "PaymentService").Methods);
        Assert.Equal((overview.TodayFromUtc, overview.TodayToUtc), sources.PaymentRange);
    }

    [Fact]
    public async Task Payment_counts_not_yet_provided_are_no_source_not_zero()
    {
        var overview = await CreateUseCase(new FakeQueries(), new FakeParking()).ExecuteAsync();   // SettlementsPendingReview = null

        var settlements = overview.Queues.SettlementsPendingReview;
        Assert.Equal(DashboardWidgetStatus.NoSource, settlements.Reason);
        Assert.Equal("PaymentService", settlements.Source);
        Assert.Null(settlements.Value);
        Assert.Contains("US-080", settlements.Detail);
        Assert.Equal(DashboardWidgetStatus.Available, overview.Kpis.RevenueToday.Status);
    }

    [Fact]
    public async Task Booking_and_data_request_widgets_follow_their_contracts()
    {
        var sources = new FakeSources();

        var overview = await CreateUseCase(new FakeQueries(), new FakeParking(), sources).ExecuteAsync();

        Assert.Equal(new BookingsTodayDto(20, 8, 6, 3, 1, 1, 1, null, null, null), Available(overview.Kpis.BookingsToday, "BookingService"));
        Assert.Equal((overview.TodayFromUtc, overview.TodayToUtc), sources.BookingRange);
        Assert.Equal(new DataSubjectRequestsDto(4, 2, 1), Available(overview.Queues.DataSubjectRequestsDue, "UserService"));
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
        var sources = new FakeSources
        {
            Occupancy = SourceResult<GateOccupancySource>.Ok(new GateOccupancySource(SourceAsOf, 0, 0, 0, 0, [])),
            Devices = SourceResult<GateDevicesSource>.Ok(new GateDevicesSource(SourceAsOf, 0, 0, 0, [])),
            Complaints = SourceResult<ComplaintSummarySource>.Ok(new ComplaintSummarySource(SourceAsOf, 0, 0, 0, 0, 0)),
            Payments = SourceResult<PaymentSummarySource>.Ok(new PaymentSummarySource(SourceAsOf, 0, 0, 0, 0, 0, 0, [])),
            Bookings = SourceResult<BookingSummarySource>.Ok(new BookingSummarySource(SourceAsOf, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)),
            DataRequests = SourceResult<DataSubjectRequestsSource>.Ok(new DataSubjectRequestsSource(SourceAsOf, 0, 0, 0)),
        };

        var overview = await CreateUseCase(queries, parking, sources).ExecuteAsync();

        Assert.Equal(0, Available(overview.Queues.PartnerApplications, "AdminService").ToReview);
        Assert.Equal(0, Available(overview.Queues.KybApplications, "ParkingService").ToReview);
        Assert.Equal(0, Available(overview.Governance.AuditLogsToday, "AdminService").Total);
        Assert.Equal(DashboardWidgetStatus.Available, overview.Queues.RiskFlags.Status);
        Assert.Equal(new ActiveSessionsDto(0, 0, 0, 0), Available(overview.Kpis.ActiveSessions, "GateService"));
        Assert.Equal(0, Available(overview.Kpis.OpenDisputes, "SupportService").Unresolved);
        Assert.Equal(0m, Available(overview.Kpis.RevenueToday, "PaymentService").Gross);
        Assert.Equal(0, Available(overview.Kpis.BookingsToday, "BookingService").Created);
        Assert.Equal(new RefundsRequestedDto(0), Available(overview.Queues.RefundsRequested, "PaymentService"));
        Assert.Equal(new SettlementsPendingReviewDto(0), Available(overview.Queues.SettlementsPendingReview, "PaymentService"));
        Assert.Equal(new DataSubjectRequestsDto(0, 0, 0), Available(overview.Queues.DataSubjectRequestsDue, "UserService"));
        Assert.Equal(0, Available(overview.Tables.GateDevices, "GateService").Total);
        Assert.Empty(Available(overview.Tables.PaymentsByMethodToday, "PaymentService").Methods);
        var lots = Available(overview.Tables.LotOccupancy, "GateService + ParkingService");
        Assert.Equal((0, 0.0), (lots.TotalActive, lots.OccupancyPercent));                     // còn bãi, không có xe → 0 thật
    }

    [Fact]
    public async Task Missing_endpoints_are_no_source_never_zero()
    {
        var overview = await CreateUseCase(new FakeQueries(), new FakeParking(), FakeSources.AllNoSource()).ExecuteAsync();

        var noSource = new (string Status, string? Reason, object? Value, DateTime? AsOf, string Source, string? Detail, string Expected)[]
        {
            Shape(overview.Kpis.RevenueToday, "PaymentService"), Shape(overview.Kpis.ActiveSessions, "GateService"),
            Shape(overview.Kpis.BookingsToday, "BookingService"), Shape(overview.Kpis.OpenDisputes, "SupportService"),
            Shape(overview.Queues.RefundsRequested, "PaymentService"), Shape(overview.Queues.SettlementsPendingReview, "PaymentService"),
            Shape(overview.Queues.DisputesAwaitingOwner, "SupportService"), Shape(overview.Queues.DataSubjectRequestsDue, "UserService"),
            Shape(overview.Tables.LotOccupancy, "GateService + ParkingService"), Shape(overview.Tables.GateDevices, "GateService"),
            Shape(overview.Tables.PaymentsByMethodToday, "PaymentService"),
        };
        foreach (var widget in noSource)
        {
            Assert.Equal(DashboardWidgetStatus.Unavailable, widget.Status);
            Assert.Equal(DashboardWidgetStatus.NoSource, widget.Reason);
            Assert.Equal(widget.Expected, widget.Source);
            Assert.Null(widget.Value);
            Assert.Null(widget.AsOf);
            Assert.Contains("chưa có", widget.Detail);
        }
        Assert.Equal(DashboardWidgetStatus.Available, overview.Queues.PartnerApplications.Status);
    }

    [Fact]
    public async Task Json_uses_lowercase_status_and_reason_and_null_value_for_unavailable()
    {
        var overview = await CreateUseCase(new FakeQueries(), new FakeParking(), new FakeSources
        {
            Payments = SourceResult<PaymentSummarySource>.NoSource("PaymentService chưa có endpoint GET /api/v1/payments/admin/summary (HTTP 404)."),
        }).ExecuteAsync();

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(overview, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var revenue = json.RootElement.GetProperty("kpis").GetProperty("revenueToday");
        Assert.Equal("unavailable", revenue.GetProperty("status").GetString());
        Assert.Equal("no_source", revenue.GetProperty("reason").GetString());
        Assert.Equal(JsonValueKind.Null, revenue.GetProperty("value").ValueKind);
        var partners = json.RootElement.GetProperty("queues").GetProperty("partnerApplications");
        Assert.Equal("available", partners.GetProperty("status").GetString());
        Assert.Equal(5, partners.GetProperty("value").GetProperty("toReview").GetInt32());
        var tables = json.RootElement.GetProperty("tables");
        Assert.True(tables.TryGetProperty("paymentsByMethodToday", out _));
        Assert.False(tables.TryGetProperty("recentPayments", out _));                          // AC2: không có giao dịch từng khách
        var sessions = json.RootElement.GetProperty("kpis").GetProperty("activeSessions").GetProperty("value");
        Assert.Equal(9, sessions.GetProperty("total").GetInt32());
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
        var sources = new FakeSources();
        var nowUtc = DateTimeOffset.Parse(now);

        var overview = await CreateUseCase(queries, new FakeParking(), sources, nowUtc).ExecuteAsync();

        var from = DateTimeOffset.Parse(expectedFrom).UtcDateTime;
        Assert.Equal(from, overview.TodayFromUtc);
        Assert.Equal(from.AddDays(1), overview.TodayToUtc);
        Assert.Equal(DateTimeKind.Utc, overview.TodayFromUtc.Kind);
        Assert.Equal((from, from.AddDays(1)), queries.AuditRange);
        Assert.Equal((from, from.AddDays(1)), sources.PaymentRange);
        Assert.Equal((from, from.AddDays(1)), sources.BookingRange);
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
        Assert.Equal(DashboardWidgetStatus.Available, overview.Tables.LotOccupancy.Status);
    }

    [Fact]
    public async Task One_failing_status_count_makes_whole_kyb_widget_source_error()
    {
        var parking = new FakeParking { Counts = { ["Submitted"] = 3, ["UnderReview"] = 1 }, FailStatus = "NeedsMoreInfo" };

        var overview = await CreateUseCase(new FakeQueries(), parking).ExecuteAsync();

        Assert.Equal(DashboardWidgetStatus.SourceError, overview.Queues.KybApplications.Reason);
        Assert.Null(overview.Queues.KybApplications.Value);                                       // không trả số thiếu
    }

    public static TheoryData<string> SourceKeys => new() { "occupancy", "devices", "lots", "complaints", "payments", "bookings", "dataRequests" };

    [Theory]
    [MemberData(nameof(SourceKeys))]
    public async Task One_failing_source_only_marks_its_own_widgets(string key)
    {
        var sources = new FakeSources().WithError(key, "Nguồn trả HTTP 500.");

        var overview = await CreateUseCase(new FakeQueries(), new FakeParking(), sources).ExecuteAsync();

        var widgets = AllExternal(overview);
        var expectedFailed = WidgetsOf(key);
        foreach (var (name, status, reason, detail) in widgets)
        {
            if (expectedFailed.Contains(name))
            {
                Assert.Equal(DashboardWidgetStatus.SourceError, reason);
                Assert.Contains("HTTP 500", detail);
            }
            else
            {
                Assert.True(status == DashboardWidgetStatus.Available || name == "settlementsPendingReview", $"{name} bị ảnh hưởng bởi {key}");
            }
        }
        Assert.Equal(DashboardWidgetStatus.Available, overview.Queues.KybApplications.Status);
        Assert.Equal(DashboardWidgetStatus.Available, overview.Governance.AuditLogsToday.Status);
    }

    [Theory]
    [MemberData(nameof(SourceKeys))]
    public async Task Slow_source_times_out_into_source_error_without_failing_others(string key)
    {
        var sources = new FakeSources();
        sources.Hang.Add(key);
        var service = ServiceOf(key);

        var overview = await CreateUseCase(new FakeQueries(), new FakeParking(), sources,
            timeouts: Timeouts(service, TimeSpan.FromMilliseconds(80), TimeSpan.FromSeconds(10))).ExecuteAsync();

        foreach (var (name, status, reason, detail) in AllExternal(overview))
        {
            if (WidgetsOf(key).Contains(name))
            {
                Assert.Equal(DashboardWidgetStatus.SourceError, reason);
                Assert.Contains("không phản hồi kịp", detail);
            }
            else Assert.True(status == DashboardWidgetStatus.Available || name == "settlementsPendingReview", $"{name} bị ảnh hưởng bởi {key}");
        }
        Assert.Contains(key, sources.ObservedCancellation);
    }

    [Theory]
    [InlineData("occupancy", "GateService")]
    [InlineData("lots", "ParkingService")]
    public async Task Lot_occupancy_needs_both_sources(string failing, string expectedSource)
    {
        var errored = await CreateUseCase(new FakeQueries(), new FakeParking(), new FakeSources().WithError(failing, "HTTP 503")).ExecuteAsync();
        var missing = await CreateUseCase(new FakeQueries(), new FakeParking(), new FakeSources().WithNoSource(failing, "chưa có endpoint X"))
            .ExecuteAsync();

        Assert.Equal((DashboardWidgetStatus.SourceError, expectedSource), (errored.Tables.LotOccupancy.Reason, errored.Tables.LotOccupancy.Source));
        Assert.Null(errored.Tables.LotOccupancy.Value);
        Assert.Equal((DashboardWidgetStatus.NoSource, expectedSource), (missing.Tables.LotOccupancy.Reason, missing.Tables.LotOccupancy.Source));
    }

    [Fact]
    public async Task Lot_occupancy_reports_error_before_missing_endpoint()
    {
        var sources = new FakeSources().WithNoSource("occupancy", "GateService chưa có endpoint.").WithError("lots", "ParkingService trả HTTP 500.");

        var overview = await CreateUseCase(new FakeQueries(), new FakeParking(), sources).ExecuteAsync();

        Assert.Equal(DashboardWidgetStatus.SourceError, overview.Tables.LotOccupancy.Reason);
        Assert.Equal("ParkingService", overview.Tables.LotOccupancy.Source);
        Assert.Equal("ParkingService trả HTTP 500.", overview.Tables.LotOccupancy.Detail);
    }

    [Fact]
    public async Task Slow_parking_service_times_out_without_failing_the_dashboard()
    {
        var parking = new FakeParking { Hang = true };

        var overview = await CreateUseCase(new FakeQueries(), parking, timeouts: Timeouts("ParkingService", TimeSpan.FromMilliseconds(80)))
            .ExecuteAsync();

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

        var overview = await CreateUseCase(new FakeQueries(), parking, timeouts: new DashboardSourceTimeouts(fallback: TimeSpan.FromSeconds(10)))
            .ExecuteAsync();

        Assert.Equal(DashboardWidgetStatus.Available, overview.Queues.KybApplications.Status);
        Assert.Equal(3, parking.MaxConcurrent);
    }

    [Fact]
    public async Task All_sources_are_called_in_parallel()
    {
        // 7 lời gọi nguồn (Gate ×2, Parking, Support, Payment, Booking, User) cùng chờ nhau – gọi nối tiếp thì hết timeout.
        var sources = new FakeSources { WaitForAll = 7 };

        var overview = await CreateUseCase(new FakeQueries(), new FakeParking(), sources,
            timeouts: new DashboardSourceTimeouts(fallback: TimeSpan.FromSeconds(10))).ExecuteAsync();

        Assert.Equal(7, sources.MaxConcurrent);
        Assert.All(AllExternal(overview), w => Assert.True(w.Status == DashboardWidgetStatus.Available || w.Name == "settlementsPendingReview"));
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
        var sources = new FakeSources();
        sources.Hang.Add("payments");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateUseCase(new FakeQueries { HonourCancellation = true }, new FakeParking { Hang = true }, sources).ExecuteAsync(cts.Token));
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
    public void Use_case_sources_and_timeouts_are_registered()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ServiceDb"] = "Host=dashboard.invalid;Database=pm_admin_test",
            ["Services:GatewayBaseUrl"] = "http://localhost:5000",
            ["Dashboard:SourceTimeoutSeconds:GateService"] = "1.5",
            ["Dashboard:SourceTimeoutSeconds:PaymentService"] = "0",
            ["Dashboard:SourceTimeoutSeconds:BookingService"] = "abc",
        }).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton(TimeProvider.System);
        services.AddAdminApplication();
        services.AddAdminInfrastructure(configuration);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.IsType<GetDashboardOverviewUseCase>(scope.ServiceProvider.GetRequiredService<IGetDashboardOverviewUseCase>());
        Assert.IsType<GateServiceClient>(scope.ServiceProvider.GetRequiredService<IGateServiceClient>());
        Assert.IsType<SupportServiceClient>(scope.ServiceProvider.GetRequiredService<ISupportServiceClient>());
        Assert.IsType<ParkingDashboardClient>(scope.ServiceProvider.GetRequiredService<IParkingDashboardClient>());
        Assert.IsType<PaymentServiceClient>(scope.ServiceProvider.GetRequiredService<IPaymentServiceClient>());
        Assert.IsType<BookingServiceClient>(scope.ServiceProvider.GetRequiredService<IBookingServiceClient>());
        Assert.IsType<UserDataRequestsClient>(scope.ServiceProvider.GetRequiredService<IUserDataRequestsClient>());
        var timeouts = scope.ServiceProvider.GetRequiredService<DashboardSourceTimeouts>();
        Assert.Equal(TimeSpan.FromSeconds(1.5), timeouts.For("GateService"));
        Assert.Equal(DashboardSourceTimeouts.Default, timeouts.For("PaymentService"));          // ≤ 0 → mặc định
        Assert.Equal(DashboardSourceTimeouts.Default, timeouts.For("BookingService"));          // sai định dạng → mặc định
        Assert.Equal(DashboardSourceTimeouts.Default, timeouts.For("SupportService"));          // không cấu hình → mặc định 3 giây
    }

    // ==================== Adapter HTTP của nguồn ====================

    [Fact]
    public async Task Parking_client_counts_kyb_from_paged_total()
    {
        Uri? requested = null;
        var handler = new StubHandler(r =>
        {
            requested = r.RequestUri;
            return Json(HttpStatusCode.OK, """{ "items": [ { "id": 1 } ], "page": 1, "pageSize": 1, "totalCount": 14, "totalPages": 14 }""");
        });
        var client = new ParkingServiceClient(Http(handler), NullLogger<ParkingServiceClient>.Instance);

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
        var client = new ParkingServiceClient(Http(new StubHandler(_ => new HttpResponseMessage(status))), NullLogger<ParkingServiceClient>.Instance);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => client.CountKybApplicationsAsync("Submitted", CancellationToken.None));
        Assert.IsType(expected, error);
    }

    [Fact]
    public async Task Gate_client_reads_occupancy_with_string_enums()
    {
        Uri? requested = null;
        var client = new GateServiceClient(Http(new StubHandler(r =>
        {
            requested = r.RequestUri;
            return Json(HttpStatusCode.OK, """
                { "asOfUtc": "2026-10-27T16:29:55Z", "total": 3, "walkIn": 1, "booked": 2, "abnormal": 0,
                  "lots": [ { "parkingLotId": 1, "activeSessions": 3, "walkIn": 1, "booked": 2, "capacity": null } ] }
                """);
        })), NullLogger<GateServiceClient>.Instance);

        var result = await client.GetOccupancyAsync(CancellationToken.None);

        Assert.Equal(SourceOutcome.Ok, result.Outcome);
        Assert.Equal(new GateLotOccupancySource(1, 3, 1, 2, null), Assert.Single(result.Value!.Lots));
        Assert.Equal(DateTimeKind.Utc, result.Value.AsOfUtc.Kind);
        Assert.Equal("http://gateway.test/api/v1/gate/admin/occupancy", requested!.ToString());
    }

    [Fact]
    public async Task Gate_client_reads_device_types_as_strings()
    {
        var client = new GateServiceClient(Http(new StubHandler(_ => Json(HttpStatusCode.OK, """
            { "asOfUtc": "2026-10-27T16:29:55Z", "online": 1, "offline": 0, "maintenance": 0, "total": 1,
              "byType": [ { "deviceType": "QrScanner", "online": 1, "offline": 0, "maintenance": 0, "total": 1 } ] }
            """))), NullLogger<GateServiceClient>.Instance);

        var result = await client.GetDevicesAsync(CancellationToken.None);

        Assert.Equal(new GateDeviceTypeSource("QrScanner", 1, 0, 0), Assert.Single(result.Value!.ByType));
    }

    [Fact]
    public async Task Payment_and_booking_clients_send_the_ict_day_as_utc_query()
    {
        var requests = new List<string>();
        var handler = new StubHandler(r =>
        {
            requests.Add(r.RequestUri!.PathAndQuery);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var from = new DateTime(2026, 10, 26, 17, 0, 0, DateTimeKind.Utc);

        await new PaymentServiceClient(Http(handler), NullLogger<PaymentServiceClient>.Instance).GetSummaryAsync(from, from.AddDays(1), default);
        await new BookingServiceClient(Http(handler), NullLogger<BookingServiceClient>.Instance).GetSummaryAsync(from, from.AddDays(1), default);
        await new SupportServiceClient(Http(handler), NullLogger<SupportServiceClient>.Instance).GetComplaintSummaryAsync(default);
        await new ParkingDashboardClient(Http(handler), NullLogger<ParkingDashboardClient>.Instance).GetLotsSummaryAsync(default);
        await new UserDataRequestsClient(Http(handler), NullLogger<UserDataRequestsClient>.Instance).GetSummaryAsync(default);

        Assert.Equal(
        [
            "/api/v1/payments/admin/summary?fromUtc=2026-10-26T17%3A00%3A00Z&toUtc=2026-10-27T17%3A00%3A00Z",
            "/api/v1/bookings/admin/summary?fromUtc=2026-10-26T17%3A00%3A00Z&toUtc=2026-10-27T17%3A00%3A00Z",
            "/api/v1/complaints/admin/summary",
            "/api/v1/parking-lots/admin/lots-summary",
            "/api/v1/users/admin/data-subject-requests/summary",
        ], requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, SourceOutcome.NoSource, "chưa có endpoint")]
    [InlineData(HttpStatusCode.NotImplemented, SourceOutcome.NoSource, "chưa có endpoint")]
    [InlineData(HttpStatusCode.Unauthorized, SourceOutcome.Error, "từ chối")]
    [InlineData(HttpStatusCode.Forbidden, SourceOutcome.Error, "từ chối")]
    [InlineData(HttpStatusCode.InternalServerError, SourceOutcome.Error, "HTTP 500")]
    [InlineData(HttpStatusCode.BadGateway, SourceOutcome.Error, "HTTP 502")]
    [InlineData(HttpStatusCode.ServiceUnavailable, SourceOutcome.Error, "HTTP 503")]
    public async Task Http_status_maps_to_source_outcome(HttpStatusCode status, SourceOutcome expected, string detail)
    {
        var client = new SupportServiceClient(Http(new StubHandler(_ => Json(status, """{ "title": "x" }"""))), NullLogger<SupportServiceClient>.Instance);

        var result = await client.GetComplaintSummaryAsync(CancellationToken.None);

        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Value);
        Assert.Contains(detail, result.Detail);
        Assert.DoesNotContain("fromUtc", result.Detail);
    }

    [Fact]
    public async Task Invalid_json_network_failure_and_http_timeout_are_source_errors()
    {
        var invalidJson = await new SupportServiceClient(Http(new StubHandler(_ => Json(HttpStatusCode.OK, "<html>"))),
            NullLogger<SupportServiceClient>.Instance).GetComplaintSummaryAsync(default);
        var network = await new SupportServiceClient(Http(new StubHandler(_ => throw new HttpRequestException("refused"))),
            NullLogger<SupportServiceClient>.Instance).GetComplaintSummaryAsync(default);
        var timedOut = await new SupportServiceClient(Http(new StubHandler(_ => throw new TaskCanceledException("HttpClient.Timeout"))),
            NullLogger<SupportServiceClient>.Instance).GetComplaintSummaryAsync(default);

        Assert.Equal((SourceOutcome.Error, "SupportService trả dữ liệu không đúng định dạng."), (invalidJson.Outcome, invalidJson.Detail));
        Assert.Equal((SourceOutcome.Error, "Không kết nối được SupportService."), (network.Outcome, network.Detail));
        Assert.Equal((SourceOutcome.Error, "SupportService không phản hồi kịp."), (timedOut.Outcome, timedOut.Detail));
    }

    [Fact]
    public async Task Caller_cancellation_propagates_from_client()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var client = new SupportServiceClient(Http(new StubHandler(_ => Json(HttpStatusCode.OK, "{}"))), NullLogger<SupportServiceClient>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetComplaintSummaryAsync(cts.Token));
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

    private static (string, string?, object?, DateTime?, string, string?, string) Shape<T>(DashboardWidget<T> w, string expected) where T : class
        => (w.Status, w.Reason, w.Value, w.AsOfUtc, w.Source, w.Detail, expected);

    /// <summary>Mọi widget lấy từ nguồn ngoài GateService / ParkingService (danh sách bãi) / Support / Payment / Booking / User.</summary>
    private static List<(string Name, string Status, string? Reason, string? Detail)> AllExternal(DashboardOverviewDto o)
    {
        (string, string, string?, string?) W<T>(string name, DashboardWidget<T> w) where T : class => (name, w.Status, w.Reason, w.Detail);
        return
        [
            W("revenueToday", o.Kpis.RevenueToday), W("activeSessions", o.Kpis.ActiveSessions), W("bookingsToday", o.Kpis.BookingsToday),
            W("openDisputes", o.Kpis.OpenDisputes), W("refundsRequested", o.Queues.RefundsRequested),
            W("settlementsPendingReview", o.Queues.SettlementsPendingReview), W("disputesAwaitingOwner", o.Queues.DisputesAwaitingOwner),
            W("dataSubjectRequestsDue", o.Queues.DataSubjectRequestsDue), W("lotOccupancy", o.Tables.LotOccupancy),
            W("gateDevices", o.Tables.GateDevices), W("paymentsByMethodToday", o.Tables.PaymentsByMethodToday),
        ];
    }

    private static string[] WidgetsOf(string key) => key switch
    {
        "occupancy" => ["activeSessions", "lotOccupancy"],
        "devices" => ["gateDevices"],
        "lots" => ["lotOccupancy"],
        "complaints" => ["openDisputes", "disputesAwaitingOwner"],
        "payments" => ["revenueToday", "refundsRequested", "settlementsPendingReview", "paymentsByMethodToday"],
        "bookings" => ["bookingsToday"],
        "dataRequests" => ["dataSubjectRequestsDue"],
        _ => throw new ArgumentOutOfRangeException(nameof(key)),
    };

    private static string ServiceOf(string key) => key switch
    {
        "occupancy" or "devices" => "GateService",
        "lots" => "ParkingService",
        "complaints" => "SupportService",
        "payments" => "PaymentService",
        "bookings" => "BookingService",
        _ => "UserService",
    };

    private static HttpClient Http(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("http://gateway.test/") };

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

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

    /// <summary>Fake cho mọi nguồn ngoài: mặc định trả số liệu nhiều bãi; từng nguồn có thể lỗi, chưa có endpoint hoặc treo.</summary>
    private sealed class FakeSources : IGateServiceClient, ISupportServiceClient, IParkingDashboardClient, IPaymentServiceClient,
        IBookingServiceClient, IUserDataRequestsClient
    {
        private int running;
        private readonly TaskCompletionSource allStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SourceResult<GateOccupancySource> Occupancy { get; set; } = SourceResult<GateOccupancySource>.Ok(new(SourceAsOf, 9, 3, 6, 1,
        [
            new(1, 5, 2, 3, 100), new(2, 4, 1, 3, null), new(3, 0, 0, 0, 40), new(99, 0, 0, 0, 20),
        ]));
        public SourceResult<GateDevicesSource> Devices { get; set; } = SourceResult<GateDevicesSource>.Ok(new(SourceAsOf, 2, 1, 1,
            [new("AnprCamera", 0, 0, 1), new("QrScanner", 2, 1, 0)]));
        public SourceResult<ParkingLotsSource> Lots { get; set; } = SourceResult<ParkingLotsSource>.Ok(new(SourceAsOf.AddSeconds(-10),
        [
            new(1, "Bãi Vincom", "TP.HCM", "Quận 1", "Active", "PmsApi", 120, false, false),
            new(2, "Bãi TSN", "TP.HCM", "Tân Bình", "Active", "Manual", 10, true, false),
            new(3, "Bãi Landmark", "TP.HCM", "Bình Thạnh", "Suspended", "Manual", 50, false, true),
            new(4, "Bãi chờ duyệt", "TP.HCM", "Quận 1", "PendingApproval", "Manual", 30, false, false),
        ]));
        public SourceResult<ComplaintSummarySource> Complaints { get; set; } =
            SourceResult<ComplaintSummarySource>.Ok(new(SourceAsOf, 2, 3, 1, 1, 1));
        public SourceResult<PaymentSummarySource> Payments { get; set; } = SourceResult<PaymentSummarySource>.Ok(new(SourceAsOf, 12,
            1_500_000m, 100_000m, 1_400_000m, 2, null, [new("Cash", 7, 600_000m), new("Vnpay", 5, 900_000m)]));
        public SourceResult<BookingSummarySource> Bookings { get; set; } =
            SourceResult<BookingSummarySource>.Ok(new(SourceAsOf, 20, 8, 6, 3, 1, 1, 1, null, null, null));
        public SourceResult<DataSubjectRequestsSource> DataRequests { get; set; } =
            SourceResult<DataSubjectRequestsSource>.Ok(new(SourceAsOf, 4, 2, 1));

        public HashSet<string> Hang { get; } = [];
        public HashSet<string> ObservedCancellation { get; } = [];
        public int WaitForAll { get; init; }
        public int MaxConcurrent { get; private set; }
        public (DateTime From, DateTime To)? PaymentRange { get; private set; }
        public (DateTime From, DateTime To)? BookingRange { get; private set; }

        public static FakeSources AllNoSource()
        {
            var sources = new FakeSources();
            foreach (var key in new[] { "occupancy", "devices", "lots", "complaints", "payments", "bookings", "dataRequests" })
                sources.WithNoSource(key, $"{ServiceOf(key)} chưa có endpoint (HTTP 404).");
            return sources;
        }

        public FakeSources WithError(string key, string detail) => Set(key, SourceOutcome.Error, detail);
        public FakeSources WithNoSource(string key, string detail) => Set(key, SourceOutcome.NoSource, detail);

        private FakeSources Set(string key, SourceOutcome outcome, string detail)
        {
            switch (key)
            {
                case "occupancy": Occupancy = new(outcome, null, detail); break;
                case "devices": Devices = new(outcome, null, detail); break;
                case "lots": Lots = new(outcome, null, detail); break;
                case "complaints": Complaints = new(outcome, null, detail); break;
                case "payments": Payments = new(outcome, null, detail); break;
                case "bookings": Bookings = new(outcome, null, detail); break;
                case "dataRequests": DataRequests = new(outcome, null, detail); break;
                default: throw new ArgumentOutOfRangeException(nameof(key));
            }
            return this;
        }

        private async Task<SourceResult<T>> Run<T>(string key, SourceResult<T> result, CancellationToken cancellationToken) where T : class
        {
            var now = Interlocked.Increment(ref running);
            lock (Hang) MaxConcurrent = Math.Max(MaxConcurrent, now);
            try
            {
                await Task.Yield();
                if (Hang.Contains(key))
                {
                    try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                    catch (OperationCanceledException) { lock (Hang) ObservedCancellation.Add(key); throw; }
                }
                if (WaitForAll > 0)
                {
                    if (now >= WaitForAll) allStarted.TrySetResult();
                    await allStarted.Task.WaitAsync(cancellationToken);
                }
                return result;
            }
            finally
            {
                Interlocked.Decrement(ref running);
            }
        }

        public Task<SourceResult<GateOccupancySource>> GetOccupancyAsync(CancellationToken cancellationToken)
            => Run("occupancy", Occupancy, cancellationToken);
        public Task<SourceResult<GateDevicesSource>> GetDevicesAsync(CancellationToken cancellationToken) => Run("devices", Devices, cancellationToken);
        public Task<SourceResult<ParkingLotsSource>> GetLotsSummaryAsync(CancellationToken cancellationToken) => Run("lots", Lots, cancellationToken);
        public Task<SourceResult<ComplaintSummarySource>> GetComplaintSummaryAsync(CancellationToken cancellationToken)
            => Run("complaints", Complaints, cancellationToken);

        Task<SourceResult<PaymentSummarySource>> IPaymentServiceClient.GetSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        {
            PaymentRange = (fromUtc, toUtc);
            return Run("payments", Payments, cancellationToken);
        }

        Task<SourceResult<BookingSummarySource>> IBookingServiceClient.GetSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        {
            BookingRange = (fromUtc, toUtc);
            return Run("bookings", Bookings, cancellationToken);
        }

        Task<SourceResult<DataSubjectRequestsSource>> IUserDataRequestsClient.GetSummaryAsync(CancellationToken cancellationToken)
            => Run("dataRequests", DataRequests, cancellationToken);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request));
        }
    }
}
