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

/// <summary>Widget chưa có nguồn: không mang giá trị.</summary>
public sealed record DashboardNoValue;

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

public sealed record DashboardKpisDto(DashboardWidget<DashboardNoValue> RevenueToday, DashboardWidget<DashboardNoValue> ActiveSessions,
    DashboardWidget<DashboardNoValue> BookingsToday, DashboardWidget<DashboardNoValue> OpenDisputes);

public sealed record DashboardQueuesDto(DashboardWidget<KybQueueDto> KybApplications, DashboardWidget<PartnerQueueDto> PartnerApplications,
    DashboardWidget<LotContractsQueueDto> LotContractsToSign, DashboardWidget<RiskFlagsDto> RiskFlags,
    DashboardWidget<DashboardNoValue> RefundsRequested, DashboardWidget<DashboardNoValue> SettlementsPendingReview,
    DashboardWidget<DashboardNoValue> DisputesAwaitingOwner, DashboardWidget<DashboardNoValue> DataSubjectRequestsDue);

public sealed record DashboardGovernanceDto(DashboardWidget<ActiveSanctionsDto> ActiveSanctions,
    DashboardWidget<ScheduledConfigChangesDto> ScheduledConfigChanges, DashboardWidget<AuditTodayDto> AuditLogsToday);

public sealed record DashboardTablesDto(DashboardWidget<DashboardNoValue> LotOccupancy, DashboardWidget<DashboardNoValue> RecentPayments,
    DashboardWidget<DashboardNoValue> GateDevices);

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
/// US-081 (UC-45): tổng quan cho Admin. Widget từ pm_admin đọc tuần tự (một DbContext); widget từ service khác (hàng đợi KYB của
/// ParkingService, qua Gateway với JWT Admin) chạy song song, mỗi nguồn timeout riêng, không retry. Một nguồn lỗi chỉ làm widget đó
/// unavailable/source_error – response vẫn 200. Widget chưa có endpoint nguồn trả unavailable/no_source (xem Dashboard - Handoff).
/// </summary>
public sealed class GetDashboardOverviewUseCase(IDashboardQueries queries, IParkingServiceClient parkingService, TimeProvider timeProvider,
    ILogger<GetDashboardOverviewUseCase> logger, TimeSpan? sourceTimeout = null) : IGetDashboardOverviewUseCase
{
    public static readonly TimeSpan DefaultSourceTimeout = TimeSpan.FromSeconds(3);

    private const string Admin = "AdminService";
    private const string Parking = "ParkingService";
    private const string Payment = "PaymentService";
    private const string Booking = "BookingService";
    private const string Gate = "GateService";
    private const string Support = "SupportService";
    private const string User = "UserService";

    private static readonly string[] KybStatuses = ["Submitted", "UnderReview", "NeedsMoreInfo"];

    public async Task<DashboardOverviewDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var (todayFrom, todayTo) = IctDay.Range(now);

        // Nguồn ngoài chạy trước, song song với các truy vấn nội bộ.
        var kybTask = LoadKybQueueAsync(cancellationToken);

        var partners = await Internal(() => queries.CountPartnerApplicationsAsync(cancellationToken), now, "hồ sơ đối tác", cancellationToken);
        var contracts = await Internal(() => queries.CountLotContractsToSignAsync(cancellationToken), now, "hợp đồng bãi", cancellationToken);
        var riskFlags = await Internal(() => queries.CountRiskFlagsAsync(now, cancellationToken), now, "risk flag", cancellationToken);
        var sanctions = await Internal(() => queries.CountActiveSanctionsAsync(now, cancellationToken), now, "chế tài", cancellationToken);
        var configs = await Internal(() => queries.GetScheduledConfigChangesAsync(now, cancellationToken), now, "tham số hệ thống", cancellationToken);
        var audit = await Internal(() => queries.CountAuditLogsAsync(todayFrom, todayTo, cancellationToken), now, "audit log", cancellationToken);

        var kyb = await kybTask;

        return new DashboardOverviewDto(now, IctDay.TimeZone, todayFrom, todayTo,
            new DashboardKpisDto(
                NoSource(Payment, "PaymentService chưa có endpoint tổng hợp doanh thu (thanh toán thành công) theo ngày cho Admin."),
                NoSource(Gate, "GateService chưa có endpoint đếm phiên gửi xe đang hoạt động toàn sàn; ParkingService chưa có tổng số chỗ."),
                NoSource(Booking, "BookingService chưa có endpoint tổng hợp booking trong ngày (tạo / hủy / no-show) cho Admin."),
                NoSource(Support, "SupportService chưa có endpoint tổng hợp tranh chấp đang mở cho Admin.")),
            new DashboardQueuesDto(kyb, partners, contracts, riskFlags,
                NoSource(Payment, "PaymentService chưa có endpoint đếm yêu cầu hoàn tiền chờ duyệt."),
                NoSource(Payment, "PaymentService chưa có endpoint đếm kỳ quyết toán chờ duyệt (US-080)."),
                NoSource(Support, "SupportService chưa có endpoint đếm tranh chấp chờ chủ bãi quá 48 giờ."),
                NoSource(User, "UserService chưa có endpoint đếm yêu cầu dữ liệu cá nhân sắp đến hạn (≤ 7 ngày).")),
            new DashboardGovernanceDto(sanctions, configs, audit),
            new DashboardTablesDto(
                NoSource(Parking, "ParkingService chưa có danh sách bãi kèm công suất (chỗ trống / tổng chỗ) cho Admin."),
                NoSource(Payment, "PaymentService chưa có danh sách thanh toán gần đây cho Admin (hiện chỉ tra theo bookingId)."),
                NoSource(Gate, "GateService chưa có endpoint tổng hợp thiết bị cổng theo loại / trạng thái và Gateway chưa định tuyến /api/v1/gate-devices.")));
    }

    private static DashboardWidget<DashboardNoValue> NoSource(string source, string detail) => DashboardWidget<DashboardNoValue>.NoSource(source, detail);

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

    /// <summary>3 trạng thái KYB gọi song song (pageSize = 1, chỉ lấy TotalCount); thiếu một số thì cả widget source_error.</summary>
    private async Task<DashboardWidget<KybQueueDto>> LoadKybQueueAsync(CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(sourceTimeout ?? DefaultSourceTimeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            var counts = await Task.WhenAll(KybStatuses.Select(s => parkingService.CountKybApplicationsAsync(s, linked.Token)));
            return DashboardWidget<KybQueueDto>.Available(new KybQueueDto(counts[0], counts[1], counts[2]), timeProvider.GetUtcNow().UtcDateTime, Parking);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Dashboard: ParkingService không phản hồi trong {Seconds} giây (hàng đợi KYB).", (sourceTimeout ?? DefaultSourceTimeout).TotalSeconds);
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
