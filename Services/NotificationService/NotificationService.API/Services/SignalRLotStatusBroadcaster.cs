using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using NotificationService.API.Hubs;
using NotificationService.Application.Features.Ingestion;

namespace NotificationService.API.Services;

/// <summary>
/// US-067/086: đẩy real-time trạng thái vận hành bãi (emergency stop, stale data) tới mọi
/// subscriber của group "lot-{id}" trên hub /hubs/parking — dashboard owner tự cập nhật
/// không cần polling. Implementation ở API layer (cần IHubContext) theo pattern
/// SignalRInAppNotificationBroadcaster; đăng ký DI BẮT BUỘC ở Program.cs.
/// </summary>
public sealed class SignalRLotStatusBroadcaster(IHubContext<ParkingHub> hubContext, ILogger<SignalRLotStatusBroadcaster> logger)
    : ILotHubBroadcaster
{
    public async Task BroadcastLotStatusAsync(LotStatusMessage message, CancellationToken cancellationToken = default)
    {
        await hubContext.Clients
            .Group(ParkingHub.LotGroup(message.ParkingLotId))
            .SendAsync("LotStatusChanged", message, cancellationToken);

        logger.LogInformation(
            "📡 LOT STATUS BROADCAST: lot={LotId}, status={Status}, emergencyStopped={EmergencyStopped}, stale={IsDataStale}",
            message.ParkingLotId, message.Status, message.EmergencyStopped, message.IsDataStale);
    }
}
