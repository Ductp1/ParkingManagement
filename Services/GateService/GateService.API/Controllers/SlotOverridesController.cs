using GateService.Application.Features.GateConsole;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.API.Controllers;

/// <summary>Body Staff ghi đè trạng thái slot: { parkingLotId, newState: "Available"|"Occupied"|"Maintenance", staffUserId, reason }.</summary>
public sealed record OverrideSlotStatusRequest(int ParkingLotId, SlotState NewState, int StaffUserId, string Reason);

/// <summary>
/// US-049 / UC-25: Staff ghi đè trạng thái slot trên sơ đồ 2D khi cảm biến hỏng hoặc sai lệch thực tế.
/// Controller chỉ điều hướng HTTP → Use case, không chứa nghiệp vụ.
/// </summary>
[ApiController]
[Route("api/v1/gate/slots")]
public sealed class SlotOverridesController(IOverrideSlotStatusUseCase overrideSlotStatus) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/gate/slots/{slotId}/override – cập nhật trạng thái slot ở ParkingService (SlotStateLog nguồn StaffOverride)
    /// và ghi GateEvents (ManualSlotOverride) kèm staff, ca trực, lý do. Không cho phép đặt Reserved.
    /// </summary>
    [HttpPost("{slotId:int}/override")]
    [ProducesResponseType<OverrideSlotStatusResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OverrideSlotStatusResultDto>> Override(
        int slotId, [FromBody] OverrideSlotStatusRequest request, CancellationToken cancellationToken)
        => Ok(await overrideSlotStatus.ExecuteAsync(
            new OverrideSlotStatusCommand(request.ParkingLotId, slotId, request.NewState, request.StaffUserId, request.Reason),
            cancellationToken));
}
