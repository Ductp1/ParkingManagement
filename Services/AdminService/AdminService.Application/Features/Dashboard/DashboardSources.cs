namespace AdminService.Application.Features.Dashboard;

// ===== Kết quả gọi một nguồn =====

public enum SourceOutcome
{
    Ok,
    /// <summary>Nguồn chưa có endpoint (HTTP 404 / 501) → widget unavailable/no_source.</summary>
    NoSource,
    /// <summary>401 / 403 / 5xx / timeout / lỗi mạng / JSON sai → widget unavailable/source_error.</summary>
    Error,
}

/// <summary>US-081: kết quả gọi một service nguồn qua Gateway – không ném exception cho lỗi của nguồn.</summary>
public sealed record SourceResult<T>(SourceOutcome Outcome, T? Value, string? Detail) where T : class
{
    public static SourceResult<T> Ok(T value) => new(SourceOutcome.Ok, value, null);
    public static SourceResult<T> NoSource(string detail) => new(SourceOutcome.NoSource, null, detail);
    public static SourceResult<T> Error(string detail) => new(SourceOutcome.Error, null, detail);
}

// ===== Hợp đồng dữ liệu của nguồn (bản sao phía AdminService, không tham chiếu project của service khác) =====
// Enum của nguồn đi trên dây dạng chuỗi (JsonStringEnumConverter trong ServiceDefaults) nên ở đây giữ string.

/// <summary>GET /api/v1/gate/admin/occupancy (GateService).</summary>
public sealed record GateOccupancySource(DateTime AsOfUtc, int Total, int WalkIn, int Booked, int Abnormal,
    IReadOnlyList<GateLotOccupancySource> Lots);

public sealed record GateLotOccupancySource(int ParkingLotId, int ActiveSessions, int WalkIn, int Booked, int? Capacity);

/// <summary>GET /api/v1/gate/admin/devices (GateService).</summary>
public sealed record GateDevicesSource(DateTime AsOfUtc, int Online, int Offline, int Maintenance, IReadOnlyList<GateDeviceTypeSource> ByType);

public sealed record GateDeviceTypeSource(string DeviceType, int Online, int Offline, int Maintenance);

/// <summary>GET /api/v1/complaints/admin/summary (SupportService).</summary>
public sealed record ComplaintSummarySource(DateTime AsOfUtc, int Open, int AwaitingOwner, int AwaitingOwnerOverdue, int OwnerResponded,
    int Escalated);

/// <summary>GET /api/v1/parking-lots/admin/lots-summary (ParkingService).</summary>
public sealed record ParkingLotsSource(DateTime AsOfUtc, IReadOnlyList<ParkingLotSource> Lots);

public sealed record ParkingLotSource(int ParkingLotId, string Name, string City, string? District, string Status, string IntegrationTier,
    int TotalSlots, bool IsEmergencyStopped, bool InstantBookingPaused);

/// <summary>
/// Contract-first – GET /api/v1/payments/admin/summary?fromUtc&amp;toUtc (PaymentService, chưa có).
/// RefundsRequested / SettlementsPendingReview = null khi PaymentService chưa đếm được (chưa có luồng hoàn tiền / US-080).
/// </summary>
public sealed record PaymentSummarySource(DateTime AsOfUtc, int SucceededCount, decimal GrossAmount, decimal RefundedAmount,
    decimal NetAmount, int? RefundsRequested, int? SettlementsPendingReview, IReadOnlyList<PaymentMethodSource> ByMethod);

public sealed record PaymentMethodSource(string Method, int Count, decimal Gross);

/// <summary>
/// Contract-first – GET /api/v1/bookings/admin/summary?fromUtc&amp;toUtc (BookingService, chưa có).
/// NoShow / CancelledByDriver / CancelledByLot = null khi BookingService chưa tách được nguyên nhân.
/// </summary>
public sealed record BookingSummarySource(DateTime AsOfUtc, int Created, int Confirmed, int Completed, int Cancelled, int CancelledNoRefund,
    int Expired, int Rejected, int? NoShow, int? CancelledByDriver, int? CancelledByLot);

/// <summary>Contract-first – GET /api/v1/users/admin/data-subject-requests/summary (UserService, chưa có). Theo DueAtUtc.</summary>
public sealed record DataSubjectRequestsSource(DateTime AsOfUtc, int Open, int DueSoon, int Overdue);

// ===== PORT (đọc, qua Gateway với JWT Admin) =====

public interface IGateServiceClient
{
    Task<SourceResult<GateOccupancySource>> GetOccupancyAsync(CancellationToken cancellationToken);
    Task<SourceResult<GateDevicesSource>> GetDevicesAsync(CancellationToken cancellationToken);
}

public interface ISupportServiceClient
{
    Task<SourceResult<ComplaintSummarySource>> GetComplaintSummaryAsync(CancellationToken cancellationToken);
}

public interface IParkingDashboardClient
{
    Task<SourceResult<ParkingLotsSource>> GetLotsSummaryAsync(CancellationToken cancellationToken);
}

public interface IPaymentServiceClient
{
    Task<SourceResult<PaymentSummarySource>> GetSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}

public interface IBookingServiceClient
{
    Task<SourceResult<BookingSummarySource>> GetSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}

public interface IUserDataRequestsClient
{
    Task<SourceResult<DataSubjectRequestsSource>> GetSummaryAsync(CancellationToken cancellationToken);
}

// ===== Timeout từng nguồn =====

/// <summary>Timeout riêng của từng service nguồn ("Dashboard:SourceTimeoutSeconds:{Service}"), mặc định 3 giây; không retry.</summary>
public sealed class DashboardSourceTimeouts(IReadOnlyDictionary<string, TimeSpan>? perService = null, TimeSpan? fallback = null)
{
    public static readonly TimeSpan Default = TimeSpan.FromSeconds(3);

    public TimeSpan For(string service)
        => perService is not null && perService.TryGetValue(service, out var timeout) && timeout > TimeSpan.Zero ? timeout : fallback ?? Default;
}
