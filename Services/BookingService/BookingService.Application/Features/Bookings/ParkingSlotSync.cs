namespace BookingService.Application.Features.Bookings;

/// <summary>
/// Đồng bộ trạng thái ô đỗ sang ParkingService và phát SignalR SlotStateChanged qua NotificationService (best-effort).
/// </summary>
public interface IParkingSlotSyncClient
{
    Task SyncSlotStateAsync(
        int parkingLotId,
        int slotId,
        string newState,
        string reason,
        int bookingId,
        CancellationToken cancellationToken = default);
}
