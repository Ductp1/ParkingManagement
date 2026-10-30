using GateService.Application.Features.LotCapacity;
using GateService.Application.Features.PlateRecognition;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.API.Controllers;

public sealed record UpdateLotCapacityRequest(
    int? Capacity, int? TransientBuffer, int? SubscriberQuota, long Version);

/// <summary>
/// US-066 – Cấu hình sức chứa &amp; vùng đệm vãng lai của bãi.
/// GET: mọi role đã đăng nhập (xem số chỗ trống). PUT: chỉ Admin (403 nếu role khác).
/// Đi qua Gateway:5000 tại prefix /api/v1/lots/{lotId}/capacity-config.
/// </summary>
[ApiController]
[Route("api/v1/lots/{lotId:int}/capacity-config")]
public class LotCapacityController(
    IGetLotCapacityConfigUseCase getLotCapacity,
    IUpdateLotCapacityConfigUseCase updateLotCapacity) : ControllerBase
{
    /// <summary>GET — cấu hình + runtime (T, V, A) + availableForTransient/availableForSubscriber (luật 5).</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<LotCapacityConfigDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<LotCapacityConfigDto>> Get(int lotId, CancellationToken cancellationToken)
        => Ok(await getLotCapacity.ExecuteAsync(lotId, cancellationToken));

    /// <summary>
    /// PUT — chỉ Admin. 400 kèm errors theo field; 409 CONFIG_VERSION_CONFLICT nếu version cũ;
    /// 200 kèm warnings[] khi hạ B &lt; V / Q &lt; A (luật 7).
    /// </summary>
    [HttpPut]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<UpdateLotCapacityResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UpdateLotCapacityResult>> Update(
        int lotId, [FromBody] UpdateLotCapacityRequest request, CancellationToken cancellationToken)
    {
        var result = await updateLotCapacity.ExecuteAsync(new UpdateLotCapacityCommand(
            lotId, request.Capacity, request.TransientBuffer, request.SubscriberQuota, request.Version), cancellationToken);
        return Ok(result);
    }
}

/// <summary>Internal API cho BookingService: đọc hạn mức thuê bao (Q) của bãi (không cần Auth nội bộ).</summary>
[ApiController]
[Route("api/v1/internal/lot-capacity")]
public class LotCapacityInternalController(IGetLotCapacityConfigUseCase getLotCapacity) : ControllerBase
{
    [HttpGet("{lotId:int}")]
    [ProducesResponseType<object>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Get(int lotId, CancellationToken cancellationToken)
    {
        var dto = await getLotCapacity.ExecuteAsync(lotId, cancellationToken);
        return Ok(new { dto.Configured, dto.Capacity, dto.TransientBuffer, dto.SubscriberQuota, dto.Version });
    }
}