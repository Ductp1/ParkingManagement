using Microsoft.AspNetCore.Mvc;
using ParkingService.Application.Features.SanctionStates;

namespace ParkingService.API.Controllers;

/// <summary>
/// US-097: trạng thái chế tài công khai của bãi (ẩn danh) – tài xế và BookingService dùng để chặn đặt chỗ mới khi bãi bị tạm ngưng.
/// Chỉ trả cờ tạm ngưng + hạn; chế tài hạ thứ hạng chỉ Admin xem (GET …/{lotId}/sanction-state).
/// </summary>
[ApiController]
[Route("api/v1/parking-lots/{lotId:int}/sanction-status")]
public sealed class LotSanctionStatusController(IGetLotSanctionStatusUseCase getStatus) : ControllerBase
{
    /// <summary>GET /api/v1/parking-lots/7/sanction-status → { isListingSuspended, listingSuspendedUntilUtc }.</summary>
    [HttpGet]
    [ProducesResponseType<LotSanctionStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotSanctionStatusDto>> Get(int lotId, CancellationToken cancellationToken)
        => Ok(await getStatus.ExecuteAsync(lotId, cancellationToken));
}
