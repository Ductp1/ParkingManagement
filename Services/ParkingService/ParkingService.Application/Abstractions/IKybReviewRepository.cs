using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Domain.Entities;

namespace ParkingService.Application.Abstractions;

/// <summary>
/// PORT ghi cho thao tác Admin trên hồ sơ KYB (US-055). Hồ sơ, bãi và sự kiện outbox (AuditRecorded,
/// ParkingLotApproved) được lưu trong CÙNG MỘT lần SaveChanges.
/// </summary>
public interface IKybReviewRepository
{
    /// <summary>Hồ sơ KYB có tracking, kèm bãi đỗ (để kích hoạt khi duyệt).</summary>
    Task<KybApplication?> GetForReviewAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Số ô đỗ đang hoạt động trong cây Zone → Floor → Slot của bãi (điều kiện "sơ đồ đầy đủ").</summary>
    Task<int> CountActiveSlotsAsync(int parkingLotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lưu thay đổi đang tracking + ghi từng sự kiện vào OutboxMessages trong một SaveChanges.
    /// Xung đột RowVersion (Admin khác vừa sửa) → ConflictException.
    /// </summary>
    Task SaveWithEventsAsync(IReadOnlyList<IntegrationEvent> events, CancellationToken cancellationToken = default);
}
