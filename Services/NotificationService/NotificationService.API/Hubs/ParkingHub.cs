using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace NotificationService.API.Hubs;

/// <summary>
/// SignalR Hub cho real-time slot state updates. Route: /hubs/parking. Module: TV6 (S1-T601).
/// Dùng SignalR Groups ("lot-{parkingLotId}") thay cho dictionary tĩnh (fix feedback PR):
///  - Mọi SendAsync đều được await → không còn fire-and-forget; lỗi gửi được ném lên caller.
///  - Disconnect → SignalR tự rời group, không còn mapping stale cần dọn thủ công.
///  - Nhiều connection cùng lot, nhiều server instance (scale-out) vẫn đúng.
/// </summary>
public sealed class ParkingHub : Hub
{
    private readonly ILogger<ParkingHub> _logger;

    public ParkingHub(ILogger<ParkingHub> logger)
    {
        _logger = logger;
    }

    /// <summary>Tên group SignalR cho 1 parking lot – dùng bởi IHubContext broadcaster.</summary>
    public static string LotGroup(int parkingLotId) => $"lot-{parkingLotId}";

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("✅ PARKING CLIENT CONNECTED: connectionId={ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // SignalR tự gỡ connection khỏi mọi group khi disconnect → không cần dọn mapping thủ công.
        _logger.LogInformation("❌ PARKING CLIENT DISCONNECTED: connectionId={ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Client subscribe nhận slot updates của 1 parking lot.</summary>
    public async Task SubscribeToLotAsync(int parkingLotId)
    {
        if (parkingLotId <= 0)
        {
            _logger.LogWarning("⚠️ SUBSCRIBE TO LOT bị bỏ qua: parkingLotId không hợp lệ ({ParkingLotId})", parkingLotId);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, LotGroup(parkingLotId));
        _logger.LogInformation("📍 SUBSCRIBED TO LOT: parkingLotId={LotId}, connectionId={ConnectionId}",
            parkingLotId, Context.ConnectionId);

        await Clients.Client(Context.ConnectionId).SendAsync("SubscribedToLot", parkingLotId);
    }

    /// <summary>Client unsubscribe khỏi lot.</summary>
    public async Task UnsubscribeFromLotAsync(int parkingLotId)
    {
        if (parkingLotId <= 0)
        {
            _logger.LogWarning("⚠️ UNSUBSCRIBE FROM LOT bị bỏ qua: parkingLotId không hợp lệ ({ParkingLotId})", parkingLotId);
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, LotGroup(parkingLotId));
        _logger.LogInformation("🚫 UNSUBSCRIBED FROM LOT: parkingLotId={LotId}, connectionId={ConnectionId}",
            parkingLotId, Context.ConnectionId);

        await Clients.Client(Context.ConnectionId).SendAsync("UnsubscribedFromLot", parkingLotId);
    }

    /// <summary>Broadcast slot state change tới mọi subscriber của lot (await group send – gửi 1 phát cho cả group).</summary>
    public async Task BroadcastSlotStateChangeAsync(int parkingLotId, SlotStateChangeMessage message)
    {
        await Clients.Group(LotGroup(parkingLotId)).SendAsync("SlotStateChanged", message);
        _logger.LogInformation("🔄 SLOT STATE CHANGED: lotId={LotId}, slotId={SlotId}, newState={NewState}",
            parkingLotId, message.SlotId, message.NewState);
    }

    /// <summary>Broadcast capacity change tới mọi subscriber của lot.</summary>
    public async Task BroadcastCapacityChangeAsync(int parkingLotId, CapacityChangeMessage message)
    {
        await Clients.Group(LotGroup(parkingLotId)).SendAsync("CapacityChanged", message);
        _logger.LogInformation("📊 CAPACITY CHANGED: lotId={LotId}, available={Available}, total={Total}",
            parkingLotId, message.AvailableSlots, message.TotalSlots);
    }
}

/// <summary>DTO cho slot state change message. Module: TV6 (S1-T601).</summary>
public sealed record SlotStateChangeMessage(
    int SlotId,
    int ParkingLotId,
    string NewState,  // "Available", "Reserved", "Occupied", "Maintenance", "Stale"
    string? PreviousState,
    string? Reason,  // "Booked", "Occupied", "Freed", "Expired", etc.
    DateTime ChangedAtUtc
);

/// <summary>DTO cho capacity message. Module: TV6 (S1-T601).</summary>
public sealed record CapacityChangeMessage(
    int ParkingLotId,
    int AvailableSlots,
    int TotalSlots,
    int OccupiedSlots,
    int ReservedSlots,
    bool IsEmergencyMode,
    DateTime UpdatedAtUtc
);
