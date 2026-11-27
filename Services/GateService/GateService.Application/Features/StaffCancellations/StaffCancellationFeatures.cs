using GateService.Application.Features.GateConsole;
using GateService.Application.Features.GateEvents;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Application.Features.StaffCancellations;

// ===== US-077: ĐẨY CẢNH BÁO SANG ADMINSERVICE =====
/// <summary>Cảnh báo Staff hủy No-show vượt ngưỡng trong cửa sổ thống kê (kèm tiêu chí đang áp dụng).</summary>
public sealed record StaffCancellationAlert(int StaffUserId, int ParkingLotId, int CancelCount, DateTime WindowFromUtc,
    DateTime WindowToUtc, int WarningThreshold, int CriticalThreshold, DateTime? LastCancelAtUtc, string? LastBookingCode);

/// <summary>
/// PORT gửi cảnh báo sang AdminService (POST /internal/v1/risk-flags/staff-abnormal-cancel, khóa nội bộ của GateService).
/// Chưa cấu hình / lỗi mạng → ném exception; bên gọi tự quyết định có nuốt lỗi hay không.
/// </summary>
public interface IAdminAlertClient
{
    Task RaiseStaffAbnormalCancelAsync(StaffCancellationAlert alert, CancellationToken cancellationToken);
}

public interface IStaffCancellationMonitor
{
    /// <summary>Kiểm tra số lần hủy No-show của Staff tại bãi trong cửa sổ; đạt ngưỡng → gửi cảnh báo. Không bao giờ ném lỗi.</summary>
    Task<bool> CheckAsync(int parkingLotId, int staffUserId, string bookingCode, CancellationToken cancellationToken);
}

/// <summary>
/// US-077 – Staff cancellation monitoring: chạy sau mỗi lần Staff hủy No-show (CancelNoShowUseCase).
/// Đếm lượt hủy của Staff tại bãi trong StaffCancellationWindowHours giờ gần nhất (GateEvents.NoShowCancel);
/// ≥ AbnormalNoShowCancelThreshold → đẩy cảnh báo sang AdminService (RiskFlag STAFF_ABNORMAL_CANCEL, AdminService tự chống trùng/nâng mức).
/// Best-effort: lỗi gửi cảnh báo chỉ ghi log, không làm hỏng thao tác hủy No-show đã hoàn tất.
/// </summary>
public sealed class StaffCancellationMonitor(
    IGateEventQueries queries,
    IAdminAlertClient adminAlerts,
    TimeProvider timeProvider,
    GateConsoleOptions options,
    ILogger<StaffCancellationMonitor> logger) : IStaffCancellationMonitor
{
    public async Task<bool> CheckAsync(int parkingLotId, int staffUserId, string bookingCode, CancellationToken cancellationToken)
    {
        try
        {
            var to = timeProvider.GetUtcNow().UtcDateTime;
            var from = to.AddHours(-Math.Max(1, options.StaffCancellationWindowHours));
            var warning = options.AbnormalNoShowCancelThreshold;
            var critical = Math.Max(warning, options.CriticalNoShowCancelThreshold);

            var stat = (await queries.NoShowCancellationStatsAsync(from, to, parkingLotId, cancellationToken))
                .FirstOrDefault(s => s.StaffUserId == staffUserId);
            if (stat is null || stat.CancelCount < warning) return false;

            await adminAlerts.RaiseStaffAbnormalCancelAsync(new StaffCancellationAlert(staffUserId, parkingLotId,
                stat.CancelCount, from, to, warning, critical, stat.LastCancelAtUtc, bookingCode), cancellationToken);
            logger.LogWarning("US-077: Staff {StaffUserId} hủy No-show {Count} lượt tại bãi {LotId} trong {Hours} giờ – đã gửi cảnh báo AdminService.",
                staffUserId, stat.CancelCount, parkingLotId, options.StaffCancellationWindowHours);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "US-077: không gửi được cảnh báo hủy No-show bất thường của Staff {StaffUserId} (bãi {LotId}).",
                staffUserId, parkingLotId);
            return false;
        }
    }
}

/// <summary>US-077: dòng thống kê hủy No-show của một Staff (trong khoảng thời gian).</summary>
public sealed record StaffCancellationStatDto(int ParkingLotId, int? StaffUserId, int CancelCount, DateTime? LastCancelAtUtc,
    bool ExceedsWarningThreshold, bool ExceedsCriticalThreshold);

/// <summary>US-077: kết quả thống kê kèm tiêu chí bất thường đang áp dụng (để AdminService ghi vào bằng chứng cảnh báo).</summary>
public sealed record StaffCancellationStatsDto(DateTime FromUtc, DateTime ToUtc, int WarningThreshold, int CriticalThreshold,
    IReadOnlyList<StaffCancellationStatDto> Items);

public interface IGetStaffCancellationStatsUseCase
{
    Task<StaffCancellationStatsDto> ExecuteAsync(DateTime? fromUtc, DateTime? toUtc, int? parkingLotId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-077: Giám sát hủy No-show bất thường của Staff (UC-24 E2, UC-32).
/// Dữ liệu lấy từ GateEvents (EventType = NoShowCancel) – nguồn mà CancelNoShowUseCase ghi sau mỗi lần hủy.
/// Tiêu chí bất thường (cấu hình "GateConsole"): Warning ≥ AbnormalNoShowCancelThreshold (5),
/// Critical ≥ CriticalNoShowCancelThreshold (10) lượt hủy của 1 Staff trong cửa sổ thời gian (mặc định 24 giờ gần nhất).
/// AdminService dùng endpoint này để tạo RiskFlag STAFF_ABNORMAL_CANCEL.
/// </summary>
public sealed class GetStaffCancellationStatsUseCase(
    IGateEventQueries queries,
    TimeProvider timeProvider,
    GateConsoleOptions options) : IGetStaffCancellationStatsUseCase
{
    public const int MaxWindowDays = 31;

    public async Task<StaffCancellationStatsDto> ExecuteAsync(DateTime? fromUtc, DateTime? toUtc, int? parkingLotId = null,
        CancellationToken cancellationToken = default)
    {
        var to = toUtc ?? timeProvider.GetUtcNow().UtcDateTime;
        var from = fromUtc ?? to.AddHours(-24);
        if (to <= from) throw new ValidationException("Khoảng thời gian thống kê không hợp lệ (toUtc phải sau fromUtc).");
        if (to - from > TimeSpan.FromDays(MaxWindowDays))
            throw new ValidationException($"Khoảng thời gian thống kê tối đa {MaxWindowDays} ngày.");
        if (parkingLotId is <= 0) throw new ValidationException("parkingLotId phải là số nguyên dương.");

        var warning = options.AbnormalNoShowCancelThreshold;
        var critical = Math.Max(warning, options.CriticalNoShowCancelThreshold);
        var rows = await queries.NoShowCancellationStatsAsync(from, to, parkingLotId, cancellationToken);
        var items = rows
            .Select(r => new StaffCancellationStatDto(r.ParkingLotId, r.StaffUserId, r.CancelCount, r.LastCancelAtUtc,
                r.CancelCount >= warning, r.CancelCount >= critical))
            .OrderByDescending(r => r.CancelCount)
            .ToList();
        return new StaffCancellationStatsDto(from, to, warning, critical, items);
    }
}
