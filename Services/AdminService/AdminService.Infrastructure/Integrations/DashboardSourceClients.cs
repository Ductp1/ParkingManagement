using System.Globalization;
using AdminService.Application.Features.Dashboard;
using Microsoft.Extensions.Logging;

namespace AdminService.Infrastructure.Integrations;

// US-081: client chỉ-đọc của Admin dashboard. BaseAddress = Gateway; JWT Admin được BearerTokenForwardingHandler chuyển tiếp.
// AdminService không tham chiếu project / DB của service khác – chỉ gọi route Gateway đã định tuyến.

/// <summary>GateService: /api/v1/gate/admin/occupancy, /api/v1/gate/admin/devices.</summary>
public sealed class GateServiceClient(HttpClient http, ILogger<GateServiceClient> logger) : IGateServiceClient
{
    private const string Service = "GateService";

    public Task<SourceResult<GateOccupancySource>> GetOccupancyAsync(CancellationToken cancellationToken)
        => DashboardSourceHttp.GetAsync<GateOccupancySource>(http, Service, "api/v1/gate/admin/occupancy", logger, cancellationToken);

    public Task<SourceResult<GateDevicesSource>> GetDevicesAsync(CancellationToken cancellationToken)
        => DashboardSourceHttp.GetAsync<GateDevicesSource>(http, Service, "api/v1/gate/admin/devices", logger, cancellationToken);
}

/// <summary>SupportService: /api/v1/complaints/admin/summary.</summary>
public sealed class SupportServiceClient(HttpClient http, ILogger<SupportServiceClient> logger) : ISupportServiceClient
{
    public Task<SourceResult<ComplaintSummarySource>> GetComplaintSummaryAsync(CancellationToken cancellationToken)
        => DashboardSourceHttp.GetAsync<ComplaintSummarySource>(http, "SupportService", "api/v1/complaints/admin/summary", logger,
            cancellationToken);
}

/// <summary>ParkingService: /api/v1/parking-lots/admin/lots-summary.</summary>
public sealed class ParkingDashboardClient(HttpClient http, ILogger<ParkingDashboardClient> logger) : IParkingDashboardClient
{
    public Task<SourceResult<ParkingLotsSource>> GetLotsSummaryAsync(CancellationToken cancellationToken)
        => DashboardSourceHttp.GetAsync<ParkingLotsSource>(http, "ParkingService", "api/v1/parking-lots/admin/lots-summary", logger,
            cancellationToken);
}

/// <summary>PaymentService (contract-first, xem Dashboard - Handoff): /api/v1/payments/admin/summary?fromUtc&amp;toUtc.</summary>
public sealed class PaymentServiceClient(HttpClient http, ILogger<PaymentServiceClient> logger) : IPaymentServiceClient
{
    public Task<SourceResult<PaymentSummarySource>> GetSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        => DashboardSourceHttp.GetAsync<PaymentSummarySource>(http, "PaymentService",
            $"api/v1/payments/admin/summary?{DashboardRange.Query(fromUtc, toUtc)}", logger, cancellationToken);
}

/// <summary>BookingService (contract-first, xem Dashboard - Handoff): /api/v1/bookings/admin/summary?fromUtc&amp;toUtc.</summary>
public sealed class BookingServiceClient(HttpClient http, ILogger<BookingServiceClient> logger) : IBookingServiceClient
{
    public Task<SourceResult<BookingSummarySource>> GetSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        => DashboardSourceHttp.GetAsync<BookingSummarySource>(http, "BookingService",
            $"api/v1/bookings/admin/summary?{DashboardRange.Query(fromUtc, toUtc)}", logger, cancellationToken);
}

/// <summary>
/// UserService (US-007): chưa có endpoint summary (contract-first trong Dashboard - Handoff) → tự đếm từ danh sách admin
/// GET /api/v1/users/data-requests?status=&amp;page= (RequireAdmin, 20 mục/trang, đã có trên develop).
/// Open = Submitted + InProgress; Overdue = đang mở và DueAtUtc &lt; now; DueSoon = đang mở và now ≤ DueAtUtc &lt; now + 24 h.
/// Khi UserService có endpoint summary thì đổi lại một lần gọi.
/// </summary>
public sealed class UserDataRequestsClient(HttpClient http, ILogger<UserDataRequestsClient> logger, TimeProvider? clock = null)
    : IUserDataRequestsClient
{
    private const string Service = "UserService";
    /// <summary>DataRequestUseCases.PageSize của UserService.</summary>
    public const int PageSize = 20;
    /// <summary>Chốt chặn: tối đa 25 trang mỗi trạng thái (500 yêu cầu đang mở).</summary>
    public const int MaxPages = 25;
    private static readonly TimeSpan DueSoonWindow = TimeSpan.FromHours(24);
    private static readonly string[] OpenStatuses = ["Submitted", "InProgress"];

    public async Task<SourceResult<DataSubjectRequestsSource>> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var dueDates = new List<DateTime>();
        foreach (var status in OpenStatuses)
        {
            for (var page = 1; page <= MaxPages; page++)
            {
                var result = await DashboardSourceHttp.GetAsync<List<DataRequestRowSource>>(http, Service,
                    $"api/v1/users/data-requests?status={status}&page={page}", logger, cancellationToken);
                if (result.Outcome == SourceOutcome.NoSource) return SourceResult<DataSubjectRequestsSource>.NoSource(result.Detail!);
                if (result.Outcome == SourceOutcome.Error) return SourceResult<DataSubjectRequestsSource>.Error(result.Detail!);

                var rows = result.Value!;
                dueDates.AddRange(rows.Select(r => DateTime.SpecifyKind(r.DueAtUtc, r.DueAtUtc.Kind == DateTimeKind.Local
                    ? DateTimeKind.Local : DateTimeKind.Utc).ToUniversalTime()));
                if (rows.Count < PageSize) break;
                if (page == MaxPages)
                    logger.LogWarning("Dashboard: UserService có hơn {Max} yêu cầu {Status} – số đếm chỉ tính {Max} trang đầu.", MaxPages * PageSize,
                        status, MaxPages);
            }
        }

        var overdue = dueDates.Count(d => d < now);
        var dueSoon = dueDates.Count(d => d >= now && d < now + DueSoonWindow);
        return SourceResult<DataSubjectRequestsSource>.Ok(new DataSubjectRequestsSource(now, dueDates.Count, dueSoon, overdue));
    }
}

internal static class DashboardRange
{
    /// <summary>fromUtc / toUtc dạng ISO 8601 UTC ("2026-11-02T17:00:00Z") – [00:00, 24:00) ICT.</summary>
    public static string Query(DateTime fromUtc, DateTime toUtc)
        => $"fromUtc={Uri.EscapeDataString(Iso(fromUtc))}&toUtc={Uri.EscapeDataString(Iso(toUtc))}";

    private static string Iso(DateTime utc)
        => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
