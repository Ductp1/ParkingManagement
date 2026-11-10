using Microsoft.AspNetCore.Mvc;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Application.Features.LotOperations;

namespace ParkingService.API.Controllers;

public sealed record EmergencyStopRequest(string Reason, bool Confirm = true);
public sealed record EmergencyResumeRequest(string Reason);

/// <summary>
/// US-067/068/069 – Vận hành bãi: dashboard tổng quan real-time, heartbeat thiết bị,
/// Emergency Stop (đóng/gỡ khẩn cấp) + lịch sử audit. Route dưới "parking-lots"
/// để Gateway (:5000) chuyển sang ParkingService (:5103).
/// Controller không bắt try/catch — ExceptionHandlingMiddleware map
/// ValidationException → 400, NotFoundException → 404.
/// </summary>
[ApiController]
[Route("api/v1/parking-lots/{lotId:int}")]
public sealed class LotOperationsController(
    IGetOperationsOverviewUseCase getOverview,
    IRecordHeartbeatUseCase recordHeartbeat,
    IEmergencyStopUseCase emergencyStop,
    ILiftEmergencyStopUseCase liftEmergencyStop,
    IGetEmergencyLogsUseCase emergencyLogs,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>GET /api/v1/parking-lots/{lotId}/dashboard – tổng quan vận hành (US-067).</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType<OperationsOverviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationsOverviewDto>> Dashboard(int lotId, CancellationToken cancellationToken)
        => Ok(await getOverview.ExecuteAsync(lotId, cancellationToken));

    /// <summary>
    /// POST /api/v1/parking-lots/{lotId}/heartbeat – thiết bị/bãi báo còn sống (US-068, 60s/lần).
    /// 200 kèm recoveredFromStale=true khi bãi vừa phục hồi (đã phát LotDataRecovered).
    /// </summary>
    [HttpPost("heartbeat")]
    [ProducesResponseType<HeartbeatResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HeartbeatResult>> Heartbeat(int lotId, CancellationToken cancellationToken)
        => Ok(await recordHeartbeat.ExecuteAsync(lotId, cancellationToken));

    /// <summary>
    /// POST /api/v1/parking-lots/{lotId}/emergency-stop – đóng bãi khẩn cấp (US-069).
    /// Body: { reason (≥5 ký tự), confirm (true) }. 200 kèm số booking bị ảnh hưởng;
    /// sự kiện LotEmergencyStopped → NotificationService fan-out đa kênh (US-086).
    /// </summary>
    [HttpPost("emergency-stop")]
    [ProducesResponseType<EmergencyStopResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmergencyStopResult>> EmergencyStop(
        int lotId, [FromBody] EmergencyStopRequest request, CancellationToken cancellationToken)
        => Ok(await emergencyStop.ExecuteAsync(lotId, ResolveActorUserId(), ResolveActorRole(),
            request.Reason, request.Confirm, cancellationToken));

    /// <summary>POST /api/v1/parking-lots/{lotId}/emergency-resume – gỡ đóng bãi khẩn cấp (US-069).</summary>
    [HttpPost("emergency-resume")]
    [ProducesResponseType<LiftEmergencyStopResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LiftEmergencyStopResult>> EmergencyResume(
        int lotId, [FromBody] EmergencyResumeRequest request, CancellationToken cancellationToken)
        => Ok(await liftEmergencyStop.ExecuteAsync(lotId, ResolveActorUserId(), ResolveActorRole(),
            request.Reason, cancellationToken));

    /// <summary>GET /api/v1/parking-lots/{lotId}/emergency-logs?take=20 – lịch sử audit Emergency Stop.</summary>
    [HttpGet("emergency-logs")]
    [ProducesResponseType<IReadOnlyList<EmergencyStopLogDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EmergencyStopLogDto>>> EmergencyLogs(
        int lotId, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
        => Ok(await emergencyLogs.ExecuteAsync(lotId, take, cancellationToken));

    /// <summary>Actor từ JWT (UserId); chưa đăng ký auth → 0 (demo/local). Role ưu tiên Admin &gt; LotOwner &gt; Staff.</summary>
    private int ResolveActorUserId() => currentUser.UserId ?? 0;

    private string ResolveActorRole()
    {
        var roles = currentUser.Roles;
        if (roles.Contains("Admin")) return "Admin";
        if (roles.Contains("LotOwner")) return "LotOwner";
        if (roles.Contains("Staff")) return "Staff";
        return roles.Count > 0 ? roles[0] : "System";
    }
}