using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Application.Features.SanctionStates;

namespace ParkingService.API.Controllers;

/// <summary>
/// US-097: AdminService đẩy hiệu lực chế tài SLA (cấp 2 hạ thứ hạng, cấp 3 tạm ngưng nhận đặt chỗ theo bãi) sang ParkingService.
/// Route dưới "parking-lots" để Gateway chuyển sang ParkingService. Không đổi ParkingLot.Status.
/// Chỉ role Admin; người thao tác lấy từ JWT (AdminService chuyển tiếp token của Admin).
/// </summary>
[ApiController]
[Route("api/v1/parking-lots")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminSanctionStatesController(
    IUpsertSanctionStateUseCase upsertState,
    ILiftSanctionStateUseCase liftState,
    IGetLotSanctionStateUseCase getLotState,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// PUT /api/v1/parking-lots/sanction-states/12 { ownerProfileId, parkingLotId?, kind, startsAtUtc, endsAtUtc? } – áp / áp lại (idempotent).
    /// </summary>
    [HttpPut("sanction-states/{sanctionId:int}")]
    [ProducesResponseType<SanctionStateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SanctionStateDto>> Upsert(int sanctionId, [FromBody] UpsertSanctionStateRequest request,
        CancellationToken cancellationToken)
        => currentUser.UserId is { } adminUserId
            ? Ok(await upsertState.ExecuteAsync(sanctionId, request, adminUserId, cancellationToken))
            : Unauthorized();

    /// <summary>DELETE /api/v1/parking-lots/sanction-states/12 – gỡ hiệu lực (idempotent, chưa từng áp cũng trả 204).</summary>
    [HttpDelete("sanction-states/{sanctionId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Lift(int sanctionId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminUserId) return Unauthorized();
        await liftState.ExecuteAsync(sanctionId, adminUserId, cancellationToken);
        return NoContent();
    }

    /// <summary>GET /api/v1/parking-lots/7/sanction-state – trạng thái đầy đủ (tạm ngưng + hạ thứ hạng, theo bãi và theo chủ bãi).</summary>
    [HttpGet("{lotId:int}/sanction-state")]
    [ProducesResponseType<LotSanctionStateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotSanctionStateDto>> GetLotState(int lotId, CancellationToken cancellationToken)
        => Ok(await getLotState.ExecuteAsync(lotId, cancellationToken));
}
