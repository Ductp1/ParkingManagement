using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace NotificationService.API.Hubs;

/// <summary>
/// SignalR Hub cho real-time slot state updates.
/// Route: /hubs/parking
/// Module: TV6 (S1-T601).
/// </summary>
public sealed class ParkingHub : Hub
{
    private readonly ILogger<ParkingHub> _logger;

    // Mapping: parkingLotId -> Set of connectionIds listening to that lot
    private static readonly Dictionary<int, HashSet<string>> LotSubscriptions = new();

    public ParkingHub(ILogger<ParkingHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("✅ PARKING CLIENT CONNECTED: connectionId={ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Unsubscribe from all lots
        var lotsToClean = new List<int>();
        lock (LotSubscriptions)
        {
            foreach (var (lotId, connections) in LotSubscriptions)
            {
                connections.Remove(Context.ConnectionId);
                if (connections.Count == 0)
                    lotsToClean.Add(lotId);
            }
            foreach (var lotId in lotsToClean)
                LotSubscriptions.Remove(lotId);
        }

        _logger.LogInformation("❌ PARKING CLIENT DISCONNECTED: connectionId={ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client subscribe đến slot updates của parking lot.
    /// </summary>
    public async Task SubscribeToLotAsync(int parkingLotId)
    {
        lock (LotSubscriptions)
        {
            if (!LotSubscriptions.ContainsKey(parkingLotId))
                LotSubscriptions[parkingLotId] = new HashSet<string>();
            LotSubscriptions[parkingLotId].Add(Context.ConnectionId);
        }

        _logger.LogInformation("📍 SUBSCRIBED TO LOT: parkingLotId={LotId}, connectionId={ConnectionId}",
            parkingLotId, Context.ConnectionId);

        // Acknowledge client
        await Clients.Client(Context.ConnectionId).SendAsync("SubscribedToLot", parkingLotId);
    }

    /// <summary>
    /// Client unsubscribe từ lot.
    /// </summary>
    public async Task UnsubscribeFromLotAsync(int parkingLotId)
    {
        lock (LotSubscriptions)
        {
            if (LotSubscriptions.TryGetValue(parkingLotId, out var connections))
            {
                connections.Remove(Context.ConnectionId);
                if (connections.Count == 0)
                    LotSubscriptions.Remove(parkingLotId);
            }
        }

        _logger.LogInformation("🚫 UNSUBSCRIBED FROM LOT: parkingLotId={LotId}, connectionId={ConnectionId}",
            parkingLotId, Context.ConnectionId);

        await Clients.Client(Context.ConnectionId).SendAsync("UnsubscribedFromLot", parkingLotId);
    }

    /// <summary>
    /// Broadcast slot state change đến tất cả subscribers của lot.
    /// Gọi từ API hoặc event handler.
    /// </summary>
    public async Task BroadcastSlotStateChangeAsync(int parkingLotId, SlotStateChangeMessage message)
    {
        lock (LotSubscriptions)
        {
            if (LotSubscriptions.TryGetValue(parkingLotId, out var connections))
            {
                foreach (var connectionId in connections)
                {
                    Clients.Client(connectionId).SendAsync("SlotStateChanged", message);
                }
                _logger.LogInformation("🔄 SLOT STATE CHANGED: lotId={LotId}, slotId={SlotId}, newState={State}, subscribers={Count}",
                    parkingLotId, message.SlotId, message.NewState, connections.Count);
            }
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Broadcast capacity change.
    /// </summary>
    public async Task BroadcastCapacityChangeAsync(int parkingLotId, CapacityChangeMessage message)
    {
        lock (LotSubscriptions)
        {
            if (LotSubscriptions.TryGetValue(parkingLotId, out var connections))
            {
                foreach (var connectionId in connections)
                {
                    Clients.Client(connectionId).SendAsync("CapacityChanged", message);
                }
                _logger.LogInformation("📊 CAPACITY CHANGED: lotId={LotId}, available={Available}, total={Total}",
                    parkingLotId, message.AvailableSlots, message.TotalSlots);
            }
        }
    }
}

/// <summary>
/// DTO cho slot state change message. Module: TV6 (S1-T601).
/// </summary>
public sealed record SlotStateChangeMessage(
    int SlotId,
    int ParkingLotId,
    string NewState,  // "Available", "Reserved", "Occupied", "Maintenance", "Stale"
    string? PreviousState,
    string? Reason,  // "Booked", "Occupied", "Freed", "Expired", etc.
    DateTime ChangedAtUtc
);

/// <summary>
/// DTO cho capacity message. Module: TV6 (S1-T601).
/// </summary>
public sealed record CapacityChangeMessage(
    int ParkingLotId,
    int AvailableSlots,
    int TotalSlots,
    int OccupiedSlots,
    int ReservedSlots,
    bool IsEmergencyMode,
    DateTime UpdatedAtUtc
);
