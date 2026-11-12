using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>
/// PORT thao tác dữ liệu Cấu hình sức chứa & vùng đệm khách vãng lai của bãi xe (UI-26, EP10).
/// </summary>
public interface ILotCapacityConfigRepository
{
    /// <summary>Lấy cấu hình sức chứa theo ParkingLotId.</summary>
    Task<LotCapacityConfig?> GetByParkingLotIdAsync(int parkingLotId, bool trackChanges = false, CancellationToken cancellationToken = default);

    Task AddAsync(LotCapacityConfig config, CancellationToken cancellationToken = default);

    Task UpdateAsync(LotCapacityConfig config, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
