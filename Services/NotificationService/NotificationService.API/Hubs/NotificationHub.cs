using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.API.Hubs;

/// <summary>
/// SignalR Hub cho notification real-time push. Route: /hubs/notify. Module: TV6 (S1-T601).
/// Dùng SignalR Groups ("user-{userId}") thay cho dictionary tĩnh:
///  - Multiple connection cùng user → cùng 1 group, không cần track thủ công.
///  - Disconnect → SignalR tự rời group, không còn stale mapping.
///  - Nhiều server instance (scale-out) vẫn đúng nhờ SignalR backplane.
/// Mọi SendAsync đều được await (không fire-and-forget).
/// </summary>
public sealed class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>Tên group SignalR cho 1 user – dùng bởi IHubContext broadcaster.</summary>
    public static string UserGroup(int userId) => $"user-{userId}";

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserIdFromContext();
        if (userId > 0)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
            _logger.LogInformation("✅ USER CONNECTED: userId={UserId}, connectionId={ConnectionId}",
                userId, Context.ConnectionId);
        }
        else
        {
            _logger.LogWarning("⚠️ CONNECT WITHOUT USERID: connectionId={ConnectionId}", Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserIdFromContext();
        if (userId > 0)
        {
            // Await để group được dọn trước khi kết thúc lifecycle disconnect.
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(userId));
            _logger.LogInformation("❌ USER DISCONNECTED: userId={UserId}, connectionId={ConnectionId}",
                userId, Context.ConnectionId);
        }

        if (exception != null)
            _logger.LogError(exception, "⚠️ HUB ERROR during disconnect");

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Gửi notification đến mọi device của 1 user (đã await từng send).</summary>
    public async Task SendNotificationToUserAsync(int userId, NotificationMessage message)
    {
        await Clients.Group(UserGroup(userId)).SendAsync("ReceiveNotification", message);
        _logger.LogInformation("📨 NOTIFY USER: userId={UserId}, channel={Channel}", userId, message.Channel);
    }

    /// <summary>Broadcast notification đến tất cả clients đang kết nối.</summary>
    public async Task BroadcastNotificationAsync(NotificationMessage message)
    {
        await Clients.All.SendAsync("ReceiveNotification", message);
        _logger.LogInformation("📢 BROADCAST NOTIFICATION: channel={Channel}", message.Channel);
    }

    /// <summary>Client heartbeat (ping/pong) để keep-alive connection.</summary>
    public async Task HeartbeatAsync()
    {
        var userId = GetUserIdFromContext();
        _logger.LogDebug("💓 HEARTBEAT: userId={UserId}, connectionId={ConnectionId}", userId, Context.ConnectionId);
        await Clients.Client(Context.ConnectionId).SendAsync("HeartbeatAck", DateTime.UtcNow);
    }

    /// <summary>Trích xuất userId từ Claims (JWT) hoặc query string.</summary>
    private int GetUserIdFromContext()
    {
        var userClaim = Context.User?.FindFirst("sub")?.Value
                     ?? Context.User?.FindFirst("nameid")?.Value
                     ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (int.TryParse(userClaim, out var userId))
            return userId;

        if (Context.GetHttpContext()?.Request.Query.TryGetValue("userId", out var userIdQuery) ?? false)
        {
            if (int.TryParse(userIdQuery.ToString(), out var userIdFromQuery))
                return userIdFromQuery;
        }

        return 0;
    }
}

/// <summary>DTO cho notification message qua SignalR. Module: TV6 (S1-T601).</summary>
public sealed record NotificationMessage(
    int NotificationId,
    int UserId,
    NotificationChannel Channel,
    string Title,
    string Body,
    string? DataJson,
    DateTime CreatedAtUtc
);
