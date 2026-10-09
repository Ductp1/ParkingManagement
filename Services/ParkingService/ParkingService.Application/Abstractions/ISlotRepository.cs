using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>
/// PORT thao tác dữ liệu Ô đỗ (Slot) trên Tầng (cấp 4 của cây phân tầng).
/// Chịu trách nhiệm về cấu trúc hình học, loại xe, kích thước ô đỗ (US-058).
/// </summary>
public interface ISlotRepository
{
    Task<Slot?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Lấy danh sách các ô đỗ của 1 tầng.</summary>
    Task<IReadOnlyList<Slot>> GetByFloorIdAsync(int floorId, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra trùng mã ô đỗ (Code) trong cùng một Tầng.</summary>
    Task<bool> ExistsCodeAsync(int floorId, string code, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra tọa độ lưới (GridX, GridY) có bị ô đỗ khác chiếm dụng trên tầng không.</summary>
    Task<bool> IsGridCellOccupiedAsync(int floorId, int gridX, int gridY, int? excludeId = null, CancellationToken cancellationToken = default);

    Task AddAsync(Slot slot, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<Slot> slots, CancellationToken cancellationToken = default);

    Task UpdateAsync(Slot slot, CancellationToken cancellationToken = default);

    Task DeleteAsync(Slot slot, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
