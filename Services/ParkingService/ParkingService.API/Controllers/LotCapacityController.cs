using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Application.Features.Capacity;

namespace ParkingService.API.Controllers;

/// <summary>
/// Controller quản lý cấu hình sức chứa & vùng đệm cho khách vãng lai (UI-26, EP10).
/// Tuyến đường: /api/v1/parking-lots/{lotId:int}/capacity-config
/// </summary>
[ApiController]
[Route("api/v1/parking-lots/{lotId:int}/capacity-config")]
public sealed class LotCapacityController(
    IGetLotCapacityConfigUseCase getLotCapacityConfig,
    IUpdateLotCapacityConfigUseCase updateLotCapacityConfig,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/parking-lots/{lotId}/capacity-config
    /// Lấy cấu hình sức chứa, hạn ngạch online và tỷ lệ vùng đệm vãng lai của bãi xe.
    /// Tự động khởi tạo cấu hình an toàn mặc định (Auto-Provisioning) nếu bãi xe mới chưa có cấu hình.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<LotCapacityConfigDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LotCapacityConfigDto>> Get(
        int lotId,
        [FromQuery] int? ownerProfileId,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerProfileId = ownerProfileId ?? currentUser.OwnerProfileId;
        var result = await getLotCapacityConfig.ExecuteAsync(lotId, effectiveOwnerProfileId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// PUT /api/v1/parking-lots/{lotId}/capacity-config
    /// Cập nhật cấu hình sức chứa & tỷ lệ vùng đệm an toàn cho khách vãng lai.
    /// </summary>
    [HttpPut]
    [ProducesResponseType<LotCapacityConfigDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LotCapacityConfigDto>> Update(
        int lotId,
        [FromBody] UpdateLotCapacityConfigRequest request,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerProfileId = request.OwnerProfileId ?? currentUser.OwnerProfileId;
        var command = new UpdateLotCapacityConfigCommand(
            ParkingLotId: lotId,
            OwnerProfileId: effectiveOwnerProfileId,
            OnlineQuota: request.OnlineQuota,
            WalkInBufferPercent: request.WalkInBufferPercent,
            OnlineLockThreshold: request.OnlineLockThreshold,
            InstantBookingPaused: request.InstantBookingPaused);

        var result = await updateLotCapacityConfig.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }
}
