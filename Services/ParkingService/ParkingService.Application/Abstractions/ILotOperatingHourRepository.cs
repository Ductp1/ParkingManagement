using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>
/// PORT thao tác dữ liệu Lịch hoạt động theo từng thứ trong tuần của bãi xe (US-062, EP09).
/// </summary>
public interface ILotOperatingHourRepository
{
    /// <summary>Lấy danh sách các ngày đã cấu hình riêng cho bãi đỗ xe.</summary>
    Task<IReadOnlyList<LotOperatingHour>> GetByParkingLotIdAsync(int parkingLotId, bool trackChanges = false, CancellationToken cancellationToken = default);

    /// <summary>Đồng bộ / ghi đè lịch hoạt động các ngày cho bãi đỗ xe (Upsert/Replace).</summary>
    Task ReplaceScheduleAsync(int parkingLotId, IEnumerable<LotOperatingHour> newSchedule, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
