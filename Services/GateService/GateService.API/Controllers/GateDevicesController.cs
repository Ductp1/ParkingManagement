using Microsoft.AspNetCore.Authorization;
using GateService.Application.Features.Shifts;
using GateService.Application.Features.GateDevices;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.API.Controllers;

/// <summary>T-703: CRUD + heartbeat của GateDevices. Controller chỉ điều hướng HTTP → Use case.</summary>
[ApiController]
[Authorize]
[Route("api/v1/gate-devices")]
public sealed class GateDevicesController(
    IGetGateDevicesUseCase listDevices,
    IGetGateDeviceByIdUseCase getDevice,
    ICreateGateDeviceUseCase createDevice,
    IUpdateGateDeviceUseCase updateDevice,
    IDeleteGateDeviceUseCase deleteDevice,
    IRecordGateDeviceHeartbeatUseCase heartbeat,
    GateAccess access) : ControllerBase
{
    /// <summary>GET /api/v1/gate-devices?parkingLotId=2&amp;status=Online – Danh sách thiết bị cổng.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GateDeviceDto>>> List(
        [FromQuery] int? parkingLotId, [FromQuery] GateDeviceStatus? status, CancellationToken cancellationToken)
    {
        if (parkingLotId is { } lotId) await access.RequireOwnerOrAdminAsync(lotId, cancellationToken);
        else if (!access.IsAdmin) return Forbid();
        return Ok(await listDevices.ExecuteAsync(parkingLotId, status, cancellationToken));
    }

    /// <summary>GET /api/v1/gate-devices/3 – Chi tiết một thiết bị.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<GateDeviceDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await OwnedAsync(id, cancellationToken));

    /// <summary>POST /api/v1/gate-devices – Đăng ký thiết bị mới.</summary>
    [HttpPost]
    public async Task<ActionResult<GateDeviceDto>> Create([FromBody] CreateGateDeviceRequest request, CancellationToken cancellationToken)
    {
        await access.RequireOwnerOrAdminAsync(request.ParkingLotId, cancellationToken);
        var created = await createDevice.ExecuteAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>PUT /api/v1/gate-devices/3 – Cập nhật thông tin thiết bị.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<GateDeviceDto>> Update(int id, [FromBody] UpdateGateDeviceRequest request, CancellationToken cancellationToken)
    {
        await OwnedAsync(id, cancellationToken);
        return Ok(await updateDevice.ExecuteAsync(id, request, cancellationToken));
    }

    /// <summary>DELETE /api/v1/gate-devices/3 – Gỡ thiết bị.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await OwnedAsync(id, cancellationToken);
        await deleteDevice.ExecuteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>POST /api/v1/gate-devices/3/heartbeat – Thiết bị báo sống: đóng dấu LastSeenAtUtc.</summary>
    [HttpPost("{id:int}/heartbeat")]
    [AllowAnonymous] // shortcut: chưa rõ thiết bị nào gọi và xác thực ra sao – khóa khi có caller thật.
    public async Task<ActionResult<GateDeviceDto>> Heartbeat(int id, CancellationToken cancellationToken)
        => Ok(await heartbeat.ExecuteAsync(id, cancellationToken));

    private async Task<GateDeviceDto> OwnedAsync(int id, CancellationToken ct)
    {
        var device = await getDevice.ExecuteAsync(id, ct);
        await access.RequireOwnerOrAdminAsync(device.ParkingLotId, ct);
        return device;
    }
}
