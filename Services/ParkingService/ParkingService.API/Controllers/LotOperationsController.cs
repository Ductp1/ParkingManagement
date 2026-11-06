using Microsoft.AspNetCore.Mvc;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Features.LotOperations;

namespace ParkingService.API.Controllers;

/// <summary>
/// US-067/068: API vận hành bãi cho chủ bãi/Staff — heartbeat (US-068) + dashboard overview real-time (US-067).
/// Phân quyền: Owner chỉ thao tác trên bãi CỦA MÌNH (so khớp OwnerProfileId claim), Staff/Admin toàn quyền.
/// </summary>
[ApiController]
[Route("api/v1/parking-lots/{lotId:int}/operations")]
public sealed class LotOperationsController(
    IRecordHeartbeatUseCase recordHeartbeat,
    IGetOperationsOverviewUseCase overview,
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
}
