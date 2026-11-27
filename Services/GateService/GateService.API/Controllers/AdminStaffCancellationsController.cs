using GateService.Application.Features.StaffCancellations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GateService.API.Controllers;

/// <summary>
/// US-077: Admin tra cứu số lượt hủy No-show theo Staff (qua Gateway, prefix /api/v1/gate). Chỉ role Admin.
/// Controller chỉ điều hướng HTTP → Use case.
/// </summary>
[ApiController]
[Route("api/v1/gate/admin/staff-cancellations")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminStaffCancellationsController(IGetStaffCancellationStatsUseCase getStats) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/gate/admin/staff-cancellations?fromUtc=...&amp;toUtc=...&amp;parkingLotId=2 – mặc định 24 giờ gần nhất, tối đa 31 ngày.
    /// Trả tiêu chí đang áp dụng (warningThreshold, criticalThreshold) + từng Staff kèm cờ vượt ngưỡng, nhiều lượt hủy nhất trước.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<StaffCancellationStatsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StaffCancellationStatsDto>> Get(
        [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, [FromQuery] int? parkingLotId, CancellationToken cancellationToken)
        => Ok(await getStats.ExecuteAsync(fromUtc, toUtc, parkingLotId, cancellationToken));
}
