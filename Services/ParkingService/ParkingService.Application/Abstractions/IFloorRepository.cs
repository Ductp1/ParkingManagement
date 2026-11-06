using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>
/// PORT thao tác dữ liệu Tầng (Floor) trong Zone (cấp 3 của cây phân tầng).
/// </summary>
public interface IFloorRepository
{
    Task<Floor?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Lấy thông tin Tầng kèm danh sách ô đỗ (Slots) bên trong.</summary>
    Task<Floor?> GetWithSlotsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Lấy danh sách các tầng trong Zone theo Level (thứ tự tầng hầm đến tầng cao).</summary>
    Task<IReadOnlyList<Floor>> GetByZoneIdAsync(int zoneId, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra trùng tên Tầng trong cùng một Zone.</summary>
    Task<bool> ExistsNameAsync(int zoneId, string name, int? excludeId = null, CancellationToken cancellationToken = default);

    Task AddAsync(Floor floor, CancellationToken cancellationToken = default);

    Task UpdateAsync(Floor floor, CancellationToken cancellationToken = default);

    Task DeleteAsync(Floor floor, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
