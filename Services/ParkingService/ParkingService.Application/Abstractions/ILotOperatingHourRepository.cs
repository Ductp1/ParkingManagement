using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>
/// PORT thao tác dữ liệu Lịch hoạt động theo từng thứ trong tuần của bãi xe (US-062, EP09).
/// </summary>
public interface ILotOperatingHourRepository
{
    /// <summary>Lấy danh sách các ngày đã cấu hình riêng cho bãi đỗ xe.</summary>
    Task<IReadOnlyList<LotOperatingHour>> GetByParkingLotIdAsync(int parkingLotId, bool trackChanges = false, CancellationToken cancellationToken = default);

    /// <summary>Lấy danh sách các ngày ghi đè (overrides) cho tuần và năm cụ thể (US-062).</summary>
    Task<IReadOnlyList<LotOperatingHourOverride>> GetOverridesByWeekAsync(int parkingLotId, int year, int weekOfYear, bool trackChanges = false, CancellationToken cancellationToken = default);

    /// <summary>Đồng bộ / ghi đè lịch hoạt động các ngày cho bãi đỗ xe (Upsert/Replace).</summary>
    Task ReplaceScheduleAsync(int parkingLotId, IEnumerable<LotOperatingHour> newSchedule, CancellationToken cancellationToken = default);

    /// <summary>Đồng bộ / ghi đè lịch hoạt động cho tuần và năm cụ thể (Upsert/Replace).</summary>
    Task ReplaceWeekOverridesAsync(int parkingLotId, int year, int weekOfYear, IEnumerable<LotOperatingHourOverride> overrides, CancellationToken cancellationToken = default);

    /// <summary>Xóa toàn bộ cấu hình ghi đè của tuần cụ thể để quay về dùng lịch cơ sở.</summary>
    Task DeleteWeekOverridesAsync(int parkingLotId, int year, int weekOfYear, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
