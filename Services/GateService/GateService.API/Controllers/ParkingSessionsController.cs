using ParkingManagement.ServiceDefaults.Security;
using GateService.Application.Features.Shifts;
using GateService.Application.Features;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.API.Controllers;

[ApiController]
[Route("api/v1/parking-sessions")]
public sealed class ParkingSessionsController(IListLotSessionsUseCase listSessions, ILookupVehicleAtGateUseCase lookup,
    GateAccess access, InternalApiKeyAuthenticator keys) : ControllerBase
{
    /// <summary>SupportService (hồ sơ tranh chấp) gọi bằng khóa nội bộ; người dùng chỉ xem bãi mình được quyền.</summary>
    private Task RequireReadAsync(int parkingLotId, CancellationToken ct)
        => keys.IsFrom(Request.Headers.Authorization, "SupportService") ? Task.CompletedTask : access.RequireReadAsync(parkingLotId, ct);

    /// <summary>GET /api/parking-sessions?parkingLotId=2&amp;status=Active – Xe đang trong bãi (UC-20).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ParkingSessionDto>>> List(
        [FromQuery] int parkingLotId, [FromQuery] ParkingSessionStatus? status, CancellationToken cancellationToken)
    {
        await RequireReadAsync(parkingLotId, cancellationToken);
        return Ok(await listSessions.ExecuteAsync(parkingLotId, status, cancellationToken));
    }

    /// <summary>
    /// GET /api/parking-sessions/lookup?parkingLotId=2&amp;plate=51A-999.99 – Tra xe tại cổng ra.
    /// 200 = xe đang trong bãi (hiện nút CHECK-OUT), 204 = chưa vào bãi (hiện nút CHECK-IN).
    /// </summary>
    [HttpGet("lookup")]
    public async Task<ActionResult<ParkingSessionDto>> Lookup([FromQuery] int parkingLotId, [FromQuery] string plate, CancellationToken cancellationToken)
    {
        await RequireReadAsync(parkingLotId, cancellationToken);
        var session = await lookup.ExecuteAsync(parkingLotId, plate, cancellationToken);
        return session is null ? NoContent() : Ok(session);
    }
}
