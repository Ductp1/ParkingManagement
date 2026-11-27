using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>Slot kèm bãi sở hữu (Slot → Floor → Zone → ParkingLot) để kiểm tra quyền thao tác theo bãi.</summary>
public sealed record SlotWithLot(Slot Slot, int ParkingLotId);

/// <summary>
/// PORT cập nhật trạng thái vận hành của Slot (Available/Reserved/Occupied/Maintenance) + ghi SlotStateLogs
/// (US-039 nhả chỗ No-show, US-049 Staff ghi đè khi cảm biến hỏng). Tách khỏi ISlotRepository (cấu trúc hình học).
/// </summary>
public interface ISlotStateRepository
{
    /// <summary>Slot đang được theo dõi (tracked) kèm ParkingLotId; null nếu không tồn tại.</summary>
    Task<SlotWithLot?> GetWithLotAsync(int slotId, CancellationToken cancellationToken = default);

    Task AddLogAsync(SlotStateLog log, CancellationToken cancellationToken = default);

    /// <summary>Lưu thay đổi; xung đột RowVersion (2 request cùng đổi 1 slot) → ConflictException.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
