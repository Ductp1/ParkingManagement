using GateService.Application.Features.GateEvents;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Application.Features.StaffCancellations;

/// <summary>US-077: dòng thống kê hủy No-show của một Staff (trong khoảng thời gian).</summary>
public sealed record StaffCancellationStatDto(int ParkingLotId, int? StaffUserId, int CancelCount, DateTime? LastCancelAtUtc,
    bool ExceedsWarningThreshold, bool ExceedsCriticalThreshold);

public interface IGetStaffCancellationStatsUseCase
{
    Task<IReadOnlyList<StaffCancellationStatDto>> ExecuteAsync(DateTime fromUtc, DateTime toUtc, int? parkingLotId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-077: Giám sát hủy No-show bất thường của Staff (UC-24 E2, UC-32).
/// Dữ liệu lấy từ GateEvents (EventType = NoShowCancel) – nguồn mà CancelNoShowUseCase ghi sau mỗi lần hủy.
/// Ngưỡng: Warning ≥ 5 lượt, Critical ≥ 10 lượt trong cửa sổ thời gian (AdminService worker dùng chính
/// endpoint này qua GateServiceClient để tạo RiskFlag alert – RuleCode STAFF_ABNORMAL_CANCEL).
/// </summary>
public sealed class GetStaffCancellationStatsUseCase(
    IGateEventQueries queries,
    TimeProvider timeProvider,
    GateConsoleOptions? options = null) : IGetStaffCancellationStatsUseCase
{
    private readonly GateConsoleOptions _options = options ?? new GateConsoleOptions();
    public const int WarningThreshold = 5;
    public const int CriticalThreshold = 10;

    public Task<IReadOnlyList<StaffCancellationStatDto>> ExecuteAsync(DateTime fromUtc, DateTime toUtc, int? parkingLotId = null,
        CancellationToken cancellationToken = default)
    {
        if (toUtc <= fromUtc) throw new ValidationException("Khoảng thời gian thống kê không hợp lệ (to phải sau from).");

        return StatsAsync(fromUtc, toUtc, parkingLotId, cancellationToken);
    }

    private async Task<IReadOnlyList<StaffCancellationStatDto>> StatsAsync(DateTime fromUtc, DateTime toUtc,
        int? parkingLotId, CancellationToken cancellationToken)
    {
        var rows = await queries.NoShowCancellationStatsAsync(fromUtc, toUtc, parkingLotId, cancellationToken);
        return rows.Select(r => new StaffCancellationStatDto(
            r.ParkingLotId, r.StaffUserId, r.CancelCount, r.LastCancelAtUtc,
            r.CancelCount >= WarningThreshold, r.CancelCount >= CriticalThreshold)).ToList();
    }
}