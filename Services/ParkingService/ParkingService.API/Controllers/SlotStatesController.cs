using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Features.Slots;

namespace ParkingService.API.Controllers;

/// <summary>Body đổi trạng thái slot – nguồn gọi: GateService (Staff ghi đè / nhả chỗ No-show), PMS, cảm biến.</summary>
public sealed record OverrideSlotStateRequest(
    int ParkingLotId,
    SlotState NewState,
    SlotStateSource Source,
    int? StaffUserId,
    string Reason,
    int? BookingId = null,
    int? ParkingSessionId = null);

/// <summary>
/// US-049 (UC-25) / US-039 (UC-24): trạng thái vận hành của slot. ParkingService là chủ dữ liệu Slot –
/// mọi thay đổi trạng thái từ service khác đi qua đây để ghi SlotStateLogs đầy đủ.
/// </summary>
[ApiController]
[Route("api/v1/parking-lots")]
public sealed class SlotStatesController(IOverrideSlotStateUseCase overrideSlotState) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/parking-lots/slots/{slotId}/state-override – đổi trạng thái slot + ghi log (trước/sau, nguồn, người đổi, lý do).
    /// Body: { parkingLotId, newState: "Available"|"Occupied"|"Maintenance"|"Reserved", source: "StaffOverride"|"Gate"|..., staffUserId?, reason }.
    /// </summary>
    [HttpPost("slots/{slotId:int}/state-override")]
    [ProducesResponseType<SlotStateOverrideResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SlotStateOverrideResultDto>> OverrideState(
        int slotId, [FromBody] OverrideSlotStateRequest request, CancellationToken cancellationToken)
        => Ok(await overrideSlotState.ExecuteAsync(new OverrideSlotStateCommand(
            slotId, request.ParkingLotId, request.NewState, request.Source, request.StaffUserId, request.Reason,
            request.BookingId, request.ParkingSessionId), cancellationToken));
}
