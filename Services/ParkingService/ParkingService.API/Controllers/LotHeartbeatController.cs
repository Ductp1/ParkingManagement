using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ParkingService.Application.Features.LotHeartbeat;

namespace ParkingService.API.Controllers;

/// <summary>
/// Controller US-068 (Heartbeat &amp; Stale Data): thiết bị/controller/chủ bãi báo heartbeat và
/// truy vấn độ tươi dữ liệu của bãi. Tuyến đường: /api/v1/parking-lots/{lotId:int}/operations
/// </summary>
[ApiController]
[Route("api/v1/parking-lots/{lotId:int}/operations")]
public sealed class LotHeartbeatController(
    IRecordLotHeartbeatUseCase recordLotHeartbeat,
    IGetLotFreshnessUseCase getLotFreshness) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/parking-lots/{lotId}/operations/heartbeat
    /// Thiết bị/controller (poll định kỳ theo LotHeartbeat:PollSeconds) hoặc chủ bãi gọi để báo dữ liệu còn sống.
    /// Nếu bãi đang stale → tự phục hồi và trả Recovered = true (kèm event LotDataRecovered qua outbox).
    /// </summary>
    [HttpPost("heartbeat")]
    [ProducesResponseType<LotHeartbeatResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotHeartbeatResultDto>> RecordHeartbeat(int lotId, CancellationToken cancellationToken)
        => Ok(await recordLotHeartbeat.ExecuteAsync(lotId, cancellationToken));

    /// <summary>
    /// GET /api/v1/parking-lots/{lotId}/operations/status
    /// Độ tươi dữ liệu hiện tại: Status = "NeverReported" | "Fresh" | "Stale" (tính theo thời gian thực).
    /// FE (driver/owner) dùng để hiển thị cảnh báo "số chỗ trống có thể không chính xác".
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType<LotFreshnessDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotFreshnessDto>> GetStatus(int lotId, CancellationToken cancellationToken)
        => Ok(await getLotFreshness.ExecuteAsync(lotId, cancellationToken));
}
