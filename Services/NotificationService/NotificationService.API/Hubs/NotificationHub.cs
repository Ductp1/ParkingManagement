using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.API.Hubs;

/// <summary>
/// SignalR Hub cho notification real-time push.
/// Route: /hubs/notify
/// Module: TV6 (S1-T601).
/// </summary>
public sealed class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    // Lưu mapping: userId -> connectionId (hỗ trợ multiple devices)
    private static readonly Dictionary<int, HashSet<string>> UserConnections = new();

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when client connects. 
    /// Client gửi kèm userId (có thể từ JWT hoặc query string).
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserIdFromContext();
        if (userId > 0)
        {
            lock (UserConnections)
            {
                if (!UserConnections.ContainsKey(userId))
                    UserConnections[userId] = new HashSet<string>();
                UserConnections[userId].Add(Context.ConnectionId);
            }

            _logger.LogInformation("✅ USER CONNECTED: userId={UserId}, connectionId={ConnectionId}", userId, Context.ConnectionId);
        }
        else
        {
            _logger.LogWarning("⚠️ CONNECT WITHOUT USERID: connectionId={ConnectionId}", Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when client disconnects.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserIdFromContext();
        if (userId > 0)
        {
            lock (UserConnections)
            {
                if (UserConnections.TryGetValue(userId, out var connections))
                {
                    connections.Remove(Context.ConnectionId);
                    if (connections.Count == 0)
                        UserConnections.Remove(userId);
                }
            }

            _logger.LogInformation("❌ USER DISCONNECTED: userId={UserId}, connectionId={ConnectionId}", userId, Context.ConnectionId);
        }

        if (exception != null)
            _logger.LogError(exception, "⚠️ HUB ERROR during disconnect");

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Gửi notification đến 1 user cụ thể (all devices).
    /// </summary>
    public async Task SendNotificationToUserAsync(int userId, NotificationMessage message)
    {
        lock (UserConnections)
        {
            if (UserConnections.TryGetValue(userId, out var connections))
            {
                foreach (var connectionId in connections)
                {
                    Clients.Client(connectionId).SendAsync("ReceiveNotification", message);
                }
                _logger.LogInformation("📨 NOTIFY USER: userId={UserId}, channel={Channel}, devices={Count}",
                    userId, message.Channel, connections.Count);
            }
        }
    }

    /// <summary>
    /// Broadcast notification đến tất cả users.
    /// </summary>
    public async Task BroadcastNotificationAsync(NotificationMessage message)
    {
        await Clients.All.SendAsync("ReceiveNotification", message);
        _logger.LogInformation("📢 BROADCAST NOTIFICATION: channel={Channel}", message.Channel);
    }

    /// <summary>
    /// Client heartbeat (ping/pong) để keep-alive connection.
    /// </summary>
    public async Task HeartbeatAsync()
    {
        var userId = GetUserIdFromContext();
        _logger.LogDebug("💓 HEARTBEAT: userId={UserId}, connectionId={ConnectionId}", userId, Context.ConnectionId);
        await Clients.Client(Context.ConnectionId).SendAsync("HeartbeatAck", DateTime.UtcNow);
    }

    /// <summary>
    /// Trích xuất userId từ Claims (JWT) hoặc query string.
    /// </summary>
    private int GetUserIdFromContext()
    {
        // Cách 1: Từ JWT claim "sub" hoặc "nameid"
        var userClaim = Context.User?.FindFirst("sub")?.Value
                     ?? Context.User?.FindFirst("nameid")?.Value
                     ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (int.TryParse(userClaim, out var userId))
            return userId;

        // Cách 2: Từ query string ?userId=123
        if (Context.GetHttpContext()?.Request.Query.TryGetValue("userId", out var userIdQuery) ?? false)
        {
            if (int.TryParse(userIdQuery.ToString(), out var userIdFromQuery))
                return userIdFromQuery;
        }

        return 0;
    }
}

/// <summary>
/// DTO cho notification message qua SignalR. Module: TV6 (S1-T601).
/// </summary>
public sealed record NotificationMessage(
    int NotificationId,
    int UserId,
    NotificationChannel Channel,
    string Title,
    string Body,
    string? DataJson,
    DateTime CreatedAtUtc
);
