using AdminService.Application.Features.LotContracts;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Application.Features.Dashboard;

// ===== DTO =====

/// <summary>Giá trị trạng thái / lý do trên dây (chuỗi cố định để Frontend so sánh).</summary>
public static class DashboardWidgetStatus
{
    public const string Available = "available";
    public const string Unavailable = "unavailable";
    /// <summary>Service nguồn chưa có endpoint cung cấp số liệu này.</summary>
    public const string NoSource = "no_source";
    /// <summary>Gọi nguồn lỗi / bị từ chối / quá thời gian chờ.</summary>
    public const string SourceError = "source_error";
}

/// <summary>
/// US-081: một widget của dashboard. Available → Value + AsOfUtc; Unavailable → Reason (no_source / source_error) + Detail tiếng Việt.
/// "0 thật" là Available với giá trị 0; "không có dữ liệu" luôn là Unavailable (Value = null), không bao giờ là số 0 giả.
/// Source = service sở hữu dữ liệu (AdminService, ParkingService, PaymentService…).
/// </summary>
public sealed record DashboardWidget<T>(string Status, T? Value, DateTime? AsOfUtc, string? Reason, string Source, string? Detail)
    where T : class
{
    public static DashboardWidget<T> Available(T value, DateTime asOfUtc, string source)
        => new(DashboardWidgetStatus.Available, value, asOfUtc, null, source, null);

    public static DashboardWidget<T> NoSource(string source, string detail)
        => new(DashboardWidgetStatus.Unavailable, null, null, DashboardWidgetStatus.NoSource, source, detail);

    public static DashboardWidget<T> SourceError(string source, string detail)
        => new(DashboardWidgetStatus.Unavailable, null, null, DashboardWidgetStatus.SourceError, source, detail);
}

public sealed record KybQueueDto(int Submitted, int UnderReview, int NeedsMoreInfo)
{
    /// <summary>Cần Admin xử lý ngay (chưa tính hồ sơ đang chờ chủ bãi bổ sung).</summary>
    public int ToReview => Submitted + UnderReview;
}

public sealed record PartnerQueueDto(int Submitted, int UnderReview, int NeedsMoreInfo, int AccountIssued)
{
    public int ToReview => Submitted + UnderReview;
}

public sealed record LotContractsQueueDto(int Pending);

/// <summary>Risk flag chưa đóng (Open + Investigating), chia theo mức độ; Overdue = quá DueAtUtc.</summary>
public sealed record RiskFlagsDto(int Open, int Investigating, int Critical, int Warning, int Info, int Overdue)
{
    public int Unresolved => Open + Investigating;
}

/// <summary>Chế tài đang hiệu lực (Active và chưa hết hạn) theo cấp.</summary>
public sealed record ActiveSanctionsDto(int Total, int Warning, int RankingPenalty, int TemporarySuspension, int PermanentBan);

public sealed record ScheduledConfigChangesDto(int Count, string? NextKey, DateTime? NextEffectiveFromUtc);

public sealed record AuditSourceCountDto(string SourceService, int Count);

public sealed record AuditTodayDto(int Total, IReadOnlyList<AuditSourceCountDto> BySource);

/// <summary>Thanh toán thành công trong ngày ICT (theo PaidAtUtc): tổng thu, đã hoàn, còn lại.</summary>
public sealed record RevenueTodayDto(int SucceededCount, decimal Gross, decimal Refunded, decimal Net);

/// <summary>Phiên gửi xe đang hoạt động toàn sàn; Abnormal đếm riêng, không cộng vào Total.</summary>
public sealed record ActiveSessionsDto(int Total, int WalkIn, int Booked, int Abnormal);

/// <summary>Booking tạo trong ngày ICT theo trạng thái; 3 nguyên nhân hủy = null khi BookingService chưa tách được.</summary>
public sealed record BookingsTodayDto(int Created, int Confirmed, int Completed, int Cancelled, int CancelledNoRefund, int Expired,
    int Rejected, int? NoShow, int? CancelledByDriver, int? CancelledByLot);

/// <summary>Tranh chấp chưa đóng (mọi category).</summary>
public sealed record OpenDisputesDto(int Open, int AwaitingOwner, int OwnerResponded, int Escalated)
{
    public int Unresolved => Open + AwaitingOwner + OwnerResponded + Escalated;
}

public sealed record RefundsRequestedDto(int Requested);

public sealed record SettlementsPendingReviewDto(int PendingReview);

/// <summary>Overdue = chờ chủ bãi đã quá hạn 48 giờ (⊂ AwaitingOwner).</summary>
public sealed record DisputesAwaitingOwnerDto(int Overdue, int AwaitingOwner);

/// <summary>Yêu cầu dữ liệu cá nhân chưa xong, theo DueAtUtc: sắp đến hạn / quá hạn.</summary>
public sealed record DataSubjectRequestsDto(int Open, int DueSoon, int Overdue);

/// <summary>
/// Một bãi: số xe đang đỗ (GateService) / sức chứa. Capacity = LotCapacityConfigs.Capacity của GateService (CapacitySource = "gate"),
/// bãi chưa cấu hình thì dùng ParkingLots.TotalSlots (CapacitySource = "parking").
/// </summary>
public sealed record LotOccupancyRowDto(int ParkingLotId, string Name, string City, string? District, string LotStatus, string IntegrationTier,
    int ActiveSessions, int Capacity, string CapacitySource, double OccupancyPercent, bool IsEmergencyStopped, bool InstantBookingPaused);

public sealed record DistrictOccupancyRowDto(string City, string? District, int LotCount, int ActiveSessions, int Capacity, double OccupancyPercent);

/// <summary>Lấp đầy theo bãi (bãi đang chờ duyệt KYB không tính) và theo quận, sắp theo % lấp đầy giảm dần.</summary>
public sealed record LotOccupancyDto(int TotalActive, int TotalCapacity, double OccupancyPercent, IReadOnlyList<LotOccupancyRowDto> Lots,
    IReadOnlyList<DistrictOccupancyRowDto> ByDistrict);

public sealed record GateDeviceTypeRowDto(string DeviceType, int Online, int Offline, int Maintenance)
{
    public int Total => Online + Offline + Maintenance;
}

public sealed record GateDevicesDto(int Online, int Offline, int Maintenance, IReadOnlyList<GateDeviceTypeRowDto> ByType)
{
    public int Total => Online + Offline + Maintenance;
}

/// <summary>Thanh toán thành công trong ngày ICT theo phương thức – chỉ số tổng, không có giao dịch / khách cụ thể (US-081 AC2).</summary>
public sealed record PaymentMethodRowDto(string Method, int Count, decimal Gross);

public sealed record PaymentsByMethodDto(IReadOnlyList<PaymentMethodRowDto> Methods);

public sealed record DashboardKpisDto(DashboardWidget<RevenueTodayDto> RevenueToday, DashboardWidget<ActiveSessionsDto> ActiveSessions,
    DashboardWidget<BookingsTodayDto> BookingsToday, DashboardWidget<OpenDisputesDto> OpenDisputes);

public sealed record DashboardQueuesDto(DashboardWidget<KybQueueDto> KybApplications, DashboardWidget<PartnerQueueDto> PartnerApplications,
    DashboardWidget<LotContractsQueueDto> LotContractsToSign, DashboardWidget<RiskFlagsDto> RiskFlags,
    DashboardWidget<RefundsRequestedDto> RefundsRequested, DashboardWidget<SettlementsPendingReviewDto> SettlementsPendingReview,
    DashboardWidget<DisputesAwaitingOwnerDto> DisputesAwaitingOwner, DashboardWidget<DataSubjectRequestsDto> DataSubjectRequestsDue);

public sealed record DashboardGovernanceDto(DashboardWidget<ActiveSanctionsDto> ActiveSanctions,
    DashboardWidget<ScheduledConfigChangesDto> ScheduledConfigChanges, DashboardWidget<AuditTodayDto> AuditLogsToday);

public sealed record DashboardTablesDto(DashboardWidget<LotOccupancyDto> LotOccupancy, DashboardWidget<GateDevicesDto> GateDevices,
    DashboardWidget<PaymentsByMethodDto> PaymentsByMethodToday);

/// <summary>US-081: tổng quan Admin Dashboard. TodayFromUtc / TodayToUtc = ngày hiện tại theo giờ Việt Nam (00:00 → 24:00 ICT).</summary>
public sealed record DashboardOverviewDto(DateTime GeneratedAtUtc, string TimeZone, DateTime TodayFromUtc, DateTime TodayToUtc,
    DashboardKpisDto Kpis, DashboardQueuesDto Queues, DashboardGovernanceDto Governance, DashboardTablesDto Tables);

// ===== PORT (đọc) =====

/// <summary>Số liệu tính trong pm_admin – đếm / gộp ở database (AsNoTracking, Count, GroupBy), không tải bản ghi về.</summary>
public interface IDashboardQueries
{
    Task<PartnerQueueDto> CountPartnerApplicationsAsync(CancellationToken cancellationToken);
    Task<LotContractsQueueDto> CountLotContractsToSignAsync(CancellationToken cancellationToken);
    Task<RiskFlagsDto> CountRiskFlagsAsync(DateTime nowUtc, CancellationToken cancellationToken);
    Task<ActiveSanctionsDto> CountActiveSanctionsAsync(DateTime nowUtc, CancellationToken cancellationToken);
    Task<ScheduledConfigChangesDto> GetScheduledConfigChangesAsync(DateTime nowUtc, CancellationToken cancellationToken);
    Task<AuditTodayDto> CountAuditLogsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}

// ===== Ngày theo giờ Việt Nam =====

public static class IctDay
{
    public const string TimeZone = "Asia/Ho_Chi_Minh";
    /// <summary>Việt Nam dùng UTC+7 quanh năm (không có giờ mùa hè).</summary>
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    /// <summary>[00:00, 24:00) ICT của ngày chứa nowUtc, đổi sang UTC.</summary>
    public static (DateTime FromUtc, DateTime ToUtc) Range(DateTime nowUtc)
    {
        var local = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc) + Offset;
        var fromUtc = DateTime.SpecifyKind(local.Date - Offset, DateTimeKind.Utc);
        return (fromUtc, fromUtc.AddDays(1));
    }
}

// ===== USE CASE =====

public interface IGetDashboardOverviewUseCase
{
    Task<DashboardOverviewDto> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// US-081 (UC-45): tổng quan cho Admin. Widget từ pm_admin đọc tuần tự (một DbContext); widget từ service khác (qua Gateway với
/// JWT Admin) chạy song song, mỗi nguồn timeout riêng, không retry. Nguồn chưa có endpoint (404 / 501) → unavailable/no_source;
/// nguồn lỗi (401 / 403 / 5xx / timeout) → unavailable/source_error – chỉ widget của nguồn đó, response vẫn 200.
/// Payment / Booking / UserService gọi theo contract đã chốt (xem Dashboard - Handoff): chủ service thêm endpoint là widget tự có số.
/// </summary>
public sealed class GetDashboardOverviewUseCase(IDashboardQueries queries, IParkingServiceClient parkingService,
    IParkingDashboardClient parkingDashboard, IGateServiceClient gateService, ISupportServiceClient supportService,
    IPaymentServiceClient paymentService, IBookingServiceClient bookingService, IUserDataRequestsClient userDataRequests,
    TimeProvider timeProvider, ILogger<GetDashboardOverviewUseCase> logger, DashboardSourceTimeouts? timeouts = null)
    : IGetDashboardOverviewUseCase
{
    private const string Admin = "AdminService";
    private const string Parking = "ParkingService";
    private const string Payment = "PaymentService";
    private const string Booking = "BookingService";
    private const string Gate = "GateService";
    private const string Support = "SupportService";
    private const string User = "UserService";

    private static readonly string[] KybStatuses = ["Submitted", "UnderReview", "NeedsMoreInfo"];

    private readonly DashboardSourceTimeouts sourceTimeouts = timeouts ?? new DashboardSourceTimeouts();

    public async Task<DashboardOverviewDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var (todayFrom, todayTo) = IctDay.Range(now);

        // Nguồn ngoài chạy trước, song song với nhau và với các truy vấn nội bộ.
        var kybTask = LoadKybQueueAsync(cancellationToken);
        var occupancyTask = Fetch(Gate, gateService.GetOccupancyAsync, cancellationToken);
        var devicesTask = Fetch(Gate, gateService.GetDevicesAsync, cancellationToken);
        var lotsTask = Fetch(Parking, parkingDashboard.GetLotsSummaryAsync, cancellationToken);
        var complaintsTask = Fetch(Support, supportService.GetComplaintSummaryAsync, cancellationToken);
        var paymentsTask = Fetch(Payment, t => paymentService.GetSummaryAsync(todayFrom, todayTo, t), cancellationToken);
        var bookingsTask = Fetch(Booking, t => bookingService.GetSummaryAsync(todayFrom, todayTo, t), cancellationToken);
        var dataRequestsTask = Fetch(User, userDataRequests.GetSummaryAsync, cancellationToken);

        var partners = await Internal(() => queries.CountPartnerApplicationsAsync(cancellationToken), now, "hồ sơ đối tác", cancellationToken);
        var contracts = await Internal(() => queries.CountLotContractsToSignAsync(cancellationToken), now, "hợp đồng bãi", cancellationToken);
        var riskFlags = await Internal(() => queries.CountRiskFlagsAsync(now, cancellationToken), now, "risk flag", cancellationToken);
        var sanctions = await Internal(() => queries.CountActiveSanctionsAsync(now, cancellationToken), now, "chế tài", cancellationToken);
        var configs = await Internal(() => queries.GetScheduledConfigChangesAsync(now, cancellationToken), now, "tham số hệ thống", cancellationToken);
        var audit = await Internal(() => queries.CountAuditLogsAsync(todayFrom, todayTo, cancellationToken), now, "audit log", cancellationToken);

        await Task.WhenAll(kybTask, occupancyTask, devicesTask, lotsTask, complaintsTask, paymentsTask, bookingsTask, dataRequestsTask);
        var occupancy = occupancyTask.Result;
        var complaints = complaintsTask.Result;
        var payments = paymentsTask.Result;

        return new DashboardOverviewDto(now, IctDay.TimeZone, todayFrom, todayTo,
            new DashboardKpisDto(
                Widget(payments, Payment, now, p => p.AsOfUtc, p => new RevenueTodayDto(p.SucceededCount, p.GrossAmount, p.RefundedAmount, p.NetAmount)),
                Widget(occupancy, Gate, now, o => o.AsOfUtc, o => new ActiveSessionsDto(o.Total, o.WalkIn, o.Booked, o.Abnormal)),
                Widget(bookingsTask.Result, Booking, now, b => b.AsOfUtc, b => new BookingsTodayDto(b.Created, b.Confirmed, b.Completed,
                    b.Cancelled, b.CancelledNoRefund, b.Expired, b.Rejected, b.NoShow, b.CancelledByDriver, b.CancelledByLot)),
                Widget(complaints, Support, now, c => c.AsOfUtc, c => new OpenDisputesDto(c.Open, c.AwaitingOwner, c.OwnerResponded, c.Escalated))),
            new DashboardQueuesDto(kybTask.Result, partners, contracts, riskFlags,
                Optional(payments, Payment, now, p => p.RefundsRequested, n => new RefundsRequestedDto(n),
                    "PaymentService chưa đếm được yêu cầu hoàn tiền chờ duyệt (luồng hoàn tiền US-034 / US-041 chưa có)."),
                Optional(payments, Payment, now, p => p.SettlementsPendingReview, n => new SettlementsPendingReviewDto(n),
                    "PaymentService chưa có kỳ quyết toán chờ duyệt (US-080 chưa có)."),
                Widget(complaints, Support, now, c => c.AsOfUtc, c => new DisputesAwaitingOwnerDto(c.AwaitingOwnerOverdue, c.AwaitingOwner)),
                Widget(dataRequestsTask.Result, User, now, d => d.AsOfUtc, d => new DataSubjectRequestsDto(d.Open, d.DueSoon, d.Overdue))),
            new DashboardGovernanceDto(sanctions, configs, audit),
            new DashboardTablesDto(
                LotOccupancy(occupancy, lotsTask.Result, now),
                Widget(devicesTask.Result, Gate, now, d => d.AsOfUtc, d => new GateDevicesDto(d.Online, d.Offline, d.Maintenance,
                    d.ByType.Select(t => new GateDeviceTypeRowDto(t.DeviceType, t.Online, t.Offline, t.Maintenance)).ToList())),
                Widget(payments, Payment, now, p => p.AsOfUtc, p => new PaymentsByMethodDto(
                    p.ByMethod.Select(m => new PaymentMethodRowDto(m.Method, m.Count, m.Gross)).OrderByDescending(m => m.Gross).ToList()))));
    }

    // ----- Nguồn ngoài -----

    /// <summary>Một lời gọi nguồn với timeout riêng; hết giờ (không phải do request bị hủy) → source_error.</summary>
    private async Task<SourceResult<T>> Fetch<T>(string service, Func<CancellationToken, Task<SourceResult<T>>> call,
        CancellationToken cancellationToken) where T : class
    {
        var timeout = sourceTimeouts.For(service);
        using var timeoutSource = new CancellationTokenSource(timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        try
        {
            return await call(linked.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Dashboard: {Service} không phản hồi trong {Seconds} giây.", service, timeout.TotalSeconds);
            return SourceResult<T>.Error($"{service} không phản hồi kịp ({timeout.TotalSeconds:0.#} giây).");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Dashboard: lỗi khi gọi {Service}.", service);
            return SourceResult<T>.Error($"Không gọi được {service}.");
        }
    }

    private static DashboardWidget<TDto> Widget<TSource, TDto>(SourceResult<TSource> result, string service, DateTime now,
        Func<TSource, DateTime> asOf, Func<TSource, TDto> map) where TSource : class where TDto : class
        => result.Outcome switch
        {
            SourceOutcome.Ok => DashboardWidget<TDto>.Available(map(result.Value!), AsOf(asOf(result.Value!), now), service),
            SourceOutcome.NoSource => DashboardWidget<TDto>.NoSource(service, result.Detail ?? $"{service} chưa có endpoint cho số liệu này."),
            _ => DashboardWidget<TDto>.SourceError(service, result.Detail ?? $"{service} hiện không phản hồi."),
        };

    /// <summary>Số nằm trong response chung nhưng nguồn trả null (chưa đếm được) → no_source, không biến thành 0.</summary>
    private static DashboardWidget<TDto> Optional<TSource, TDto>(SourceResult<TSource> result, string service, DateTime now,
        Func<TSource, int?> pick, Func<int, TDto> map, string missingDetail) where TSource : class where TDto : class
        => result.Outcome == SourceOutcome.Ok && pick(result.Value!) is null
            ? DashboardWidget<TDto>.NoSource(service, missingDetail)
            : Widget(result, service, now, _ => now, s => map(pick(s)!.Value));

    /// <summary>Nguồn không gửi AsOfUtc (giá trị mặc định) → dùng thời điểm dựng dashboard.</summary>
    private static DateTime AsOf(DateTime sourceAsOf, DateTime now) => sourceAsOf == default ? now : DateTime.SpecifyKind(sourceAsOf, DateTimeKind.Utc);

    /// <summary>
    /// Ghép số xe đang đỗ (GateService) với danh sách bãi (ParkingService) theo ParkingLotId; cần cả 2 nguồn. Nguồn lỗi được báo trước
    /// nguồn chưa có endpoint. Bãi chờ duyệt KYB (PendingApproval) không tính; bãi GateService có mà ParkingService không có thì bỏ qua.
    /// </summary>
    private static DashboardWidget<LotOccupancyDto> LotOccupancy(SourceResult<GateOccupancySource> occupancy, SourceResult<ParkingLotsSource> lots,
        DateTime now)
    {
        if (occupancy.Outcome != SourceOutcome.Ok || lots.Outcome != SourceOutcome.Ok)
        {
            var failed = new[]
                {
                    (Service: Gate, Outcome: occupancy.Outcome, Detail: occupancy.Detail),
                    (Service: Parking, Outcome: lots.Outcome, Detail: lots.Detail),
                }
                .Where(s => s.Outcome != SourceOutcome.Ok).ToList();
            var errors = failed.Where(s => s.Outcome == SourceOutcome.Error).ToList();
            var reported = errors.Count > 0 ? errors : failed;
            var source = string.Join(" + ", reported.Select(s => s.Service));
            var detail = string.Join(" ", reported.Select(s => s.Detail ?? $"{s.Service} chưa có endpoint cho số liệu này."));
            return errors.Count > 0
                ? DashboardWidget<LotOccupancyDto>.SourceError(source, detail)
                : DashboardWidget<LotOccupancyDto>.NoSource(source, detail);
        }

        var perLot = occupancy.Value!.Lots.ToDictionary(l => l.ParkingLotId);
        var rows = lots.Value!.Lots
            .Where(l => l.Status != "PendingApproval")
            .Select(l =>
            {
                var gate = perLot.GetValueOrDefault(l.ParkingLotId);
                var (capacity, capacitySource) = gate?.Capacity is { } c ? (c, "gate") : (l.TotalSlots, "parking");
                var active = gate?.ActiveSessions ?? 0;
                return new LotOccupancyRowDto(l.ParkingLotId, l.Name, l.City, l.District, l.Status, l.IntegrationTier, active, capacity,
                    capacitySource, Percent(active, capacity), l.IsEmergencyStopped, l.InstantBookingPaused);
            })
            .OrderByDescending(r => r.OccupancyPercent).ThenBy(r => r.Name, StringComparer.Ordinal)
            .ToList();
        var byDistrict = rows.GroupBy(r => (r.City, r.District))
            .Select(g => new DistrictOccupancyRowDto(g.Key.City, g.Key.District, g.Count(), g.Sum(r => r.ActiveSessions), g.Sum(r => r.Capacity),
                Percent(g.Sum(r => r.ActiveSessions), g.Sum(r => r.Capacity))))
            .OrderByDescending(d => d.OccupancyPercent).ThenBy(d => d.District ?? "", StringComparer.Ordinal)
            .ToList();
        var totalActive = rows.Sum(r => r.ActiveSessions);
        var totalCapacity = rows.Sum(r => r.Capacity);
        var asOf = new[] { AsOf(occupancy.Value.AsOfUtc, now), AsOf(lots.Value.AsOfUtc, now) }.Min();
        return DashboardWidget<LotOccupancyDto>.Available(
            new LotOccupancyDto(totalActive, totalCapacity, Percent(totalActive, totalCapacity), rows, byDistrict), asOf,
            $"{Gate} + {Parking}");
    }

    private static double Percent(int active, int capacity) => capacity > 0 ? Math.Round(active * 100.0 / capacity, 1) : 0;

    // ----- pm_admin -----

    /// <summary>Truy vấn pm_admin lỗi (mất kết nối…) → chỉ widget đó source_error; hủy request thì để lỗi nổi lên.</summary>
    private async Task<DashboardWidget<T>> Internal<T>(Func<Task<T>> load, DateTime now, string what, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return DashboardWidget<T>.Available(await load(), now, Admin);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Dashboard: không đọc được số liệu {What} trong pm_admin.", what);
            return DashboardWidget<T>.SourceError(Admin, $"Không đọc được số liệu {what} của AdminService.");
        }
    }

    // ----- Hàng đợi KYB (ParkingService, giữ hành vi US-081 ban đầu) -----

    /// <summary>3 trạng thái KYB gọi song song (pageSize = 1, chỉ lấy TotalCount); thiếu một số thì cả widget source_error.</summary>
    private async Task<DashboardWidget<KybQueueDto>> LoadKybQueueAsync(CancellationToken cancellationToken)
    {
        var timeout = sourceTimeouts.For(Parking);
        using var timeoutSource = new CancellationTokenSource(timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        try
        {
            var counts = await Task.WhenAll(KybStatuses.Select(s => parkingService.CountKybApplicationsAsync(s, linked.Token)));
            return DashboardWidget<KybQueueDto>.Available(new KybQueueDto(counts[0], counts[1], counts[2]), timeProvider.GetUtcNow().UtcDateTime, Parking);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Dashboard: ParkingService không phản hồi trong {Seconds} giây (hàng đợi KYB).", timeout.TotalSeconds);
            return DashboardWidget<KybQueueDto>.SourceError(Parking, "ParkingService không phản hồi kịp, chưa lấy được hàng đợi KYB.");
        }
        catch (Exception ex) when (ex is AuthenticationException or ForbiddenException)
        {
            logger.LogWarning("Dashboard: ParkingService từ chối truy vấn hàng đợi KYB ({Error}).", ex.GetType().Name);
            return DashboardWidget<KybQueueDto>.SourceError(Parking, "ParkingService từ chối truy vấn hàng đợi KYB (phiên hoặc quyền không hợp lệ).");
        }
        catch (Exception ex) when (ex is DependencyUnavailableException or ConflictException or NotFoundException)
        {
            logger.LogWarning("Dashboard: ParkingService lỗi khi đếm hàng đợi KYB ({Error}).", ex.GetType().Name);
            return DashboardWidget<KybQueueDto>.SourceError(Parking, "ParkingService hiện không phản hồi, chưa lấy được hàng đợi KYB.");
        }
    }
}
