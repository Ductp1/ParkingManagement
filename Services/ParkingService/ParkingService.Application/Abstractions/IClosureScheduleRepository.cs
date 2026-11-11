using ParkingManagement.SharedKernel.Enums;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>
/// PORT thao tác dữ liệu lịch tạm đóng một phần hoặc toàn bộ bãi đỗ (US-063, US-069).
/// Quản lý vòng đời ClosureSchedule, kiểm tra trùng lặp thời gian và ghi nhật ký trạng thái ô đỗ (SlotStateLogs).
/// </summary>
public interface IClosureScheduleRepository
{
    Task<ClosureSchedule?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách lịch tạm đóng của một bãi xe, có thể lọc theo TargetType và trạng thái hiệu lực.
    /// </summary>
    Task<IReadOnlyList<ClosureSchedule>> GetByParkingLotIdAsync(
        int parkingLotId,
        ClosureTargetType? targetType = null,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra xem có lịch đóng nào đang hoạt động trùng lặp thời gian trên cùng target hay không.
    /// </summary>
    Task<bool> HasOverlappingClosureAsync(
        int parkingLotId,
        ClosureTargetType targetType,
        int targetId,
        DateTime startsAtUtc,
        DateTime? endsAtUtc,
        int? excludeId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(ClosureSchedule schedule, CancellationToken cancellationToken = default);

    Task UpdateAsync(ClosureSchedule schedule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi nhật ký chuyển trạng thái các ô đỗ khi đóng/mở lại bãi.
    /// </summary>
    Task AddStateLogsAsync(IEnumerable<SlotStateLog> logs, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
