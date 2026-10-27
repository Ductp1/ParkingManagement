using ParkingManagement.SharedKernel.Enums;

namespace GateService.Application.Features.GateConsole;

/// <summary>
/// Cấu hình Gate Console đọc từ appsettings (mục "GateConsole"). Giá trị mặc định khớp
/// SystemConfigs của AdminDb: CHECKIN_GRACE_PERIOD_MINUTES = 15, PARKING_BILLING_GRACE_PERIOD_MINUTES = 15.
/// </summary>
public sealed class GateConsoleOptions
{
    public const string SectionName = "GateConsole";

    /// <summary>US-044: tài xế đến trễ tối đa này phút thì booking vẫn check-in bình thường (tính phí từ giờ đã đặt).</summary>
    public int CheckInGracePeriodMinutes { get; init; } = 15;

    /// <summary>US-046: fallback Billing Grace cho khách vãng lai (booking có snapshot riêng sẽ dùng giá trị của snapshot).</summary>
    public int BillingGracePeriodMinutes { get; init; } = 15;
}

/// <summary>
/// Thông tin booking đã xác thực từ BookingService mà Gate Console cần khi check-in/chốt phí (UC-21, UC-22).
/// Là bản đọc không sở hữu: GateService KHÔNG lưu booking, mọi nghiệp vụ trạng thái thuộc BookingService.
/// </summary>
public sealed record GateBookingSnapshot(
    string Code,
    string Status,
    int ParkingLotId,
    string PlateNumber,
    string VehicleType,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string? SlotCode,
    decimal PrepaidAmount,
    int BillingGracePeriodMinutes,
    decimal OverstayMultiplier);

/// <summary>UC-20: 1 booking trong ca trực (đã lọc thông tin riêng tư – không SĐT/CCCD).</summary>
public sealed record GateBookingListItem(
    string Code,
    string PlateNumber,
    string VehicleType,
    string? SlotCode,
    DateTime StartAtUtc,
    DateTime EndAtUtc,
    string Status,
    bool IsOverdue,
    decimal TotalAmount);

/// <summary>
/// Port gọi BookingService (TV3) từ GateService (TV7): tra cứu booking + đồng bộ trạng thái khi cổng check-in/out.
/// Infrastructure cài bằng HttpClient; lỗi mạng/hết hạn → DependencyUnavailableException (HTTP 503).
/// </summary>
public interface IGateBookingClient
{
    /// <summary>Tra booking theo mã. Trả null khi BookingService báo 404 (mã sai) — lỗi hạ tầng thì ném exception.</summary>
    Task<GateBookingSnapshot?> FindByCodeAsync(string bookingCode, CancellationToken cancellationToken = default);

    /// <summary>UC-20: booking của 1 bãi trong khung giờ ca trực (đầu vào đã chuẩn hoá hoa, tìm theo mã/biển số).</summary>
    Task<IReadOnlyList<GateBookingListItem>> ListForLotAsync(int parkingLotId, DateTime fromUtc, DateTime toUtc,
        string? search, CancellationToken cancellationToken = default);

    /// <summary>Đồng bộ booking → CheckedIn sau khi lượt gửi xe được tạo (idempotent phía BookingService).</summary>
    Task SyncCheckInAsync(string bookingCode, int? staffUserId, CancellationToken cancellationToken = default);

    /// <summary>Đồng bộ booking → Completed sau khi chốt phí và xe ra cổng.</summary>
    Task SyncCheckOutAsync(string bookingCode, int? staffUserId, CancellationToken cancellationToken = default);
}