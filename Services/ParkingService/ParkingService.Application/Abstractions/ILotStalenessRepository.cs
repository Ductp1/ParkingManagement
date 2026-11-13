using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>
/// PORT phục vụ US-068 (Heartbeat &amp; Stale Data): đọc/ghi heartbeat của bãi xe và lưu outbox event
/// cùng transaction. Tách khỏi IParkingLotRepository để không ảnh hưởng các use case tìm kiếm/chi tiết.
/// </summary>
public interface ILotStalenessRepository
{
    /// <summary>Lấy bãi ở chế độ tracked (EF ChangeTracker) để ghi heartbeat/mark stale.</summary>
    Task<ParkingLot?> GetByIdForUpdateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Danh sách bãi Active (tracked) – đầu vào của StalenessMonitorWorker mỗi lượt quét.</summary>
    Task<IReadOnlyList<ParkingLot>> ListActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Ghi các integration event vào bảng OutboxMessages rồi SaveChanges trong cùng transaction.</summary>
    Task SaveWithEventsAsync(IReadOnlyList<IntegrationEvent> events, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
