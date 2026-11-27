using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc;
using NotificationService.API.Hubs;

namespace NotificationService.API.Controllers;

/// <summary>
/// US-066: internal API nhận yêu cầu broadcast CapacityChanged từ GateService
/// (repo chưa có message broker → dùng HTTP internal; lỗi trả 500 để caller log, không bao giờ
/// làm hỏng nghiệp vụ chính vì GateService publisher tự nuốt mọi response).
/// </summary>
[ApiController]
[Route("api/v1/internal/parking")]
public sealed class ParkingBroadcastController(IHubContext<ParkingHub> hubContext, ILogger<ParkingBroadcastController> logger) : ControllerBase
{
    public sealed record CapacityBroadcastRequest(
        int LotId, int Capacity, int TransientBuffer, int SubscriberQuota,
        int AvailableForTransient, int AvailableForSubscriber, long Version, DateTime TimestampUtc);

    [HttpPost("broadcast-capacity")]
    public async Task<IActionResult> BroadcastCapacity([FromBody] CapacityBroadcastRequest request, CancellationToken cancellationToken)
    {
        await hubContext.Clients
            .Group(ParkingHub.LotGroup(request.LotId))
            .SendAsync("CapacityChanged", new CapacityConfigChangedMessage(
                request.LotId, request.Capacity, request.TransientBuffer, request.SubscriberQuota,
                request.AvailableForTransient, request.AvailableForSubscriber, request.Version, request.TimestampUtc),
            cancellationToken);
        logger.LogInformation("US-066: broadcast CapacityChanged lot {LotId} version {Version}", request.LotId, request.Version);
        return Ok(new { status = "Broadcasted" });
    }
}