using Microsoft.AspNetCore.Mvc;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Features.LotOperations;

namespace ParkingService.API.Controllers;

/// <summary>
/// US-067/068/069: API vận hành bãi cho chủ bãi/Staff — heartbeat, dashboard overview, emergency stop.
/// Phân quyền: Owner chỉ thao tác trên bãi CỦA MÌNH (so khớp OwnerProfileId claim), Staff/Admin toàn quyền.
/// </summary>
[ApiController]
[Route("api/v1/parking-lots/{lotId:int}/operations")]
public sealed class LotOperationsController(
    IRecordHeartbeatUseCase recordHeartbeat,
    IGetOperationsOverviewUseCase overview,
    IEmergencyStopUseCase emergencyStop,
    ILiftEmergencyStopUseCase liftEmergencyStop,
    ILotOperationsRepository repository,
    ICurrentUser currentUser) : ControllerBase
{
    private async Task EnsureAccessAsync(int lotId, CancellationToken cancellationToken)
    {
        var lot = await repository.GetLotAsync(lotId, cancellationToken)
                  ?? throw new NotFoundException("ParkingLot", lotId);
        var isOwner = currentUser.Roles.Contains("LotOwner") && currentUser.OwnerProfileId == lot.OwnerProfileId;
        var isStaffOrAdmin = currentUser.Roles.Contains("Staff") || currentUser.Roles.Contains("Admin");
        if (!isOwner && !isStaffOrAdmin)
            throw new ForbiddenException("Bạn không có quyền vận hành bãi này.");
    }

    /// <summary>POST /api/v1/parking-lots/{lotId}/operations/heartbeat – US-068: thiết bị/controller báo còn sống.</summary>
    [HttpPost("heartbeat")]
    public async Task<ActionResult<HeartbeatResultDto>> Heartbeat(int lotId, CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(lotId, cancellationToken);
        return Ok(await recordHeartbeat.ExecuteAsync(lotId, cancellationToken));
    }

    /// <summary>GET /api/v1/parking-lots/{lotId}/operations/overview – US-067: dashboard real-time (occupancy, booking, freshness).</summary>
    [HttpGet("overview")]
    public async Task<ActionResult<LotOperationsOverviewDto>> Overview(int lotId, CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(lotId, cancellationToken);
        return Ok(await overview.ExecuteAsync(lotId, cancellationToken));
    }

    public sealed record EmergencyStopRequest(string Reason, bool Confirm);
    public sealed record LiftRequest(string Reason);

    /// <summary>POST /api/v1/parking-lots/{lotId}/operations/emergency-stop – US-069: đóng bãi khẩn cấp (confirm bắt buộc).</summary>
    [HttpPost("emergency-stop")]
    public async Task<ActionResult<EmergencyStopResultDto>> EmergencyStop(
        int lotId, [FromBody] EmergencyStopRequest request, CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(lotId, cancellationToken);
        var result = await emergencyStop.ExecuteAsync(lotId, currentUser.UserId ?? 0,
            currentUser.Roles.FirstOrDefault() ?? "Unknown", request.Reason, request.Confirm, cancellationToken);
        return Ok(result);
    }

    /// <summary>POST /api/v1/parking-lots/{lotId}/operations/emergency-stop/lift – US-069: gỡ đóng bãi.</summary>
    [HttpPost("emergency-stop/lift")]
    public async Task<ActionResult<EmergencyStopResultDto>> Lift(
        int lotId, [FromBody] LiftRequest request, CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(lotId, cancellationToken);
        var result = await liftEmergencyStop.ExecuteAsync(lotId, currentUser.UserId ?? 0,
            currentUser.Roles.FirstOrDefault() ?? "Unknown", request.Reason, cancellationToken);
        return Ok(result);
    }

    /// <summary>GET /api/v1/parking-lots/{lotId}/operations/status – nội bộ service-to-service (US-069): BookingService check trước khi tạo booking.</summary>
    [HttpGet("status")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public async Task<ActionResult> Status(int lotId, CancellationToken cancellationToken)
    {
        var lot = await repository.GetLotAsync(lotId, cancellationToken)
                  ?? throw new NotFoundException("ParkingLot", lotId);
        return Ok(new
        {
            lotId,
            status = lot.Status.ToString(),
            emergencyStopped = lot.Status == ParkingLotStatus.Suspended && lot.EmergencyStoppedAtUtc is not null,
            emergencyStopReason = lot.EmergencyStopReason,
        });
    }

    /// <summary>GET /api/v1/parking-lots/{lotId}/operations/emergency-logs – US-069: audit trail.</summary>
    [HttpGet("emergency-logs")]
    public async Task<ActionResult> Logs(int lotId, CancellationToken cancellationToken)
    {
        await EnsureAccessAsync(lotId, cancellationToken);
        var logs = await repository.GetEmergencyLogsAsync(lotId, 50, cancellationToken);
        return Ok(logs.Select(l => new { l.Id, l.Action, l.ActorUserId, l.ActorRole, l.Reason, l.AffectedBookings, l.CreatedAtUtc }));
    }
}
