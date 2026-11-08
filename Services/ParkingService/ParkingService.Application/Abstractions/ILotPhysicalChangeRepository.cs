using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>
/// PORT thao tác dữ liệu đợt thay đổi vật lý và giám sát SLA 48h (US-061).
/// </summary>
public interface ILotPhysicalChangeRepository
{
    Task<LotPhysicalChange?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LotPhysicalChange>> GetByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LotPhysicalChange>> GetActiveByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LotPhysicalChange>> GetOverdueCandidatesAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
    Task AddAsync(LotPhysicalChange change, CancellationToken cancellationToken = default);
    Task UpdateAsync(LotPhysicalChange change, CancellationToken cancellationToken = default);
    Task<ParkingLot?> GetLotByIdAsync(int parkingLotId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
