using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>
/// PORT thao tác dữ liệu Khu vực / Tòa nhà (Zone) trong bãi (cấp 2 của cây phân tầng).
/// </summary>
public interface IZoneRepository
{
    Task<Zone?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Lấy danh sách các Zone của bãi đỗ theo thứ tự hiển thị SortOrder.</summary>
    Task<IReadOnlyList<Zone>> GetByParkingLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra trùng mã Zone trong cùng một bãi đỗ.</summary>
    Task<bool> ExistsCodeAsync(int parkingLotId, string code, int? excludeId = null, CancellationToken cancellationToken = default);

    Task AddAsync(Zone zone, CancellationToken cancellationToken = default);

    Task UpdateAsync(Zone zone, CancellationToken cancellationToken = default);

    Task DeleteAsync(Zone zone, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
