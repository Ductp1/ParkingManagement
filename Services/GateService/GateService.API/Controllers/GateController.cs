using GateService.Application.Features;
using GateService.Application.Features.GateConsole;
using Microsoft.AspNetCore.Mvc;

namespace GateService.API.Controllers;

/// <summary>T-702: API của Gate Console – cổng vào/ra. Controller chỉ điều hướng HTTP → Use case, không chứa nghiệp vụ.</summary>
[ApiController]
[Route("api/v1/gate")]
public sealed class GateController(ICheckInUseCase checkIn, ICheckOutUseCase checkOut) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/gate/check-in – Check-in tại cổng vào.
    /// Body: { parkingLotId, method: "Qr" | "BookingCode" | "Manual", plate, bookingCode?, staffUserId? }.
    /// Khách vãng lai = không kèm bookingCode.
    /// </summary>
    [HttpPost("check-in")]
    public async Task<ActionResult<ParkingSessionDto>> CheckIn([FromBody] CheckInCommand command, CancellationToken cancellationToken)
        => Ok(await checkIn.ExecuteAsync(command, cancellationToken));

    /// <summary>
    /// POST /api/v1/gate/check-out – Check-out tại cổng ra.
    /// Body: { parkingLotId, plate, method: "Qr" | "Manual" (mặc định Manual), staffUserId? }.
    /// </summary>
    [HttpPost("check-out")]
    public async Task<ActionResult<ParkingSessionDto>> CheckOut([FromBody] CheckOutCommand command, CancellationToken cancellationToken)
        => Ok(await checkOut.ExecuteAsync(command, cancellationToken));
}
