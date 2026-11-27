using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>
/// PORT quản trị bãi đỗ xe cho phía chủ bãi (TV5).
/// Hỗ trợ tạo bãi mới, cập nhật, truy vấn bãi của chủ và eager load cây phân cấp (Lot → Zone → Floor → Slot).
/// </summary>
public interface IParkingLotManagementRepository
{
    Task<ParkingLot?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Lấy bãi kèm theo toàn bộ cây Zone → Floor → Slot (Hợp đồng API số 4).</summary>
    Task<ParkingLot?> GetHierarchyAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Lấy danh sách các bãi thuộc quyền sở hữu của chủ bãi (OwnerProfileId).</summary>
    Task<IReadOnlyList<ParkingLot>> GetByOwnerProfileIdAsync(int ownerProfileId, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra xem chủ bãi đã có bãi nào trùng tên chưa.</summary>
    Task<bool> ExistsByNameAsync(int ownerProfileId, string name, int? excludeId = null, CancellationToken cancellationToken = default);

    Task AddAsync(ParkingLot lot, CancellationToken cancellationToken = default);

    Task UpdateAsync(ParkingLot lot, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
