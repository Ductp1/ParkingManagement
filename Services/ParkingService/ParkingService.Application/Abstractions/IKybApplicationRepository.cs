using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>
/// PORT thao tác dữ liệu hồ sơ thẩm định KYB 4 bước của bãi đỗ xe (US-054).
/// </summary>
public interface IKybApplicationRepository
{
    Task<KybApplication?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Lấy hồ sơ thẩm định mới nhất của bãi đỗ xe (kèm thông tin ParkingLot).</summary>
    Task<KybApplication?> GetLatestByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default);

    /// <summary>Lấy toàn bộ lịch sử các lần nộp hồ sơ KYB của bãi đỗ theo thứ tự giảm dần.</summary>
    Task<IReadOnlyList<KybApplication>> GetHistoryByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default);

    Task AddAsync(KybApplication application, CancellationToken cancellationToken = default);

    Task UpdateAsync(KybApplication application, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
