using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Application.Features.OperatingHours;

namespace ParkingService.API.Controllers;

/// <summary>
/// Controller quản lý lịch hoạt động theo từng thứ trong tuần của bãi xe (US-062, EP09).
/// Tuyến đường: /api/v1/parking-lots/{lotId:int}/operating-hours
/// </summary>
[ApiController]
[Route("api/v1/parking-lots/{lotId:int}/operating-hours")]
public sealed class LotOperatingHoursController(
    IGetLotOperatingHoursUseCase getLotOperatingHours,
    IUpdateLotOperatingHoursUseCase updateLotOperatingHours,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/parking-lots/{lotId}/operating-hours
    /// Lấy lịch hoạt động đầy đủ cả tuần (7 ngày từ Thứ Hai đến Chủ Nhật) của bãi xe.
    /// Hỗ trợ cả Lịch tuần cơ sở và Lịch riêng theo tuần cụ thể (?year=2026&weekOfYear=46).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<LotOperatingHoursWeeklyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LotOperatingHoursWeeklyDto>> Get(
        int lotId,
        [FromQuery] int? ownerProfileId = null,
        [FromQuery] int? year = null,
        [FromQuery] int? weekOfYear = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveOwnerProfileId = ownerProfileId ?? currentUser.OwnerProfileId;
        var result = await getLotOperatingHours.ExecuteAsync(lotId, effectiveOwnerProfileId, year, weekOfYear, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// PUT /api/v1/parking-lots/{lotId}/operating-hours
    /// Cập nhật / đồng bộ lịch hoạt động các ngày trong tuần của bãi xe (Batch Sync/Replace).
    /// Nếu có Year & WeekOfYear: Lưu vào lịch ngoại lệ tuần đó. Nếu không có: Lưu vào lịch cơ sở.
    /// </summary>
    [HttpPut]
    [ProducesResponseType<LotOperatingHoursWeeklyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LotOperatingHoursWeeklyDto>> Update(
        int lotId,
        [FromBody] UpdateLotOperatingHoursRequest request,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerProfileId = request.OwnerProfileId ?? currentUser.OwnerProfileId;
        var command = new UpdateLotOperatingHoursCommand(
            ParkingLotId: lotId,
            OwnerProfileId: effectiveOwnerProfileId,
            Schedule: request.Schedule,
            Year: request.Year,
            WeekOfYear: request.WeekOfYear,
            Reason: request.Reason);

        var result = await updateLotOperatingHours.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// DELETE /api/v1/parking-lots/{lotId}/operating-hours/overrides
    /// Xóa toàn bộ cấu hình lịch ngoại lệ của tuần cụ thể để khôi phục về lịch cơ sở.
    /// </summary>
    [HttpDelete("overrides")]
    [ProducesResponseType<LotOperatingHoursWeeklyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LotOperatingHoursWeeklyDto>> DeleteOverride(
        int lotId,
        [FromQuery] int year,
        [FromQuery] int weekOfYear,
        [FromQuery] int? ownerProfileId,
        [FromServices] IDeleteLotOperatingHoursOverrideUseCase deleteOverrideUseCase,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerProfileId = ownerProfileId ?? currentUser.OwnerProfileId;
        var result = await deleteOverrideUseCase.ExecuteAsync(lotId, year, weekOfYear, effectiveOwnerProfileId, cancellationToken);
        return Ok(result);
    }
}
