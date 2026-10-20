using Microsoft.AspNetCore.Mvc;
using ParkingService.Application.Features.Layouts;

namespace ParkingService.API.Controllers;

/// <summary>
/// Controller quản lý sơ đồ lưới mặt bằng 2D của tầng (T-502, US-059).
/// Phục vụ component LayoutRenderer (SVG) cho Driver, Owner dashboard và Gate console.
/// </summary>
[ApiController]
[Route("api/v1/parking-lots")]
public sealed class FloorLayoutsController(IGetFloorLayoutUseCase getFloorLayout) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/parking-lots/floors/{floorId}/layout
    /// Lấy sơ đồ mặt bằng lưới 2D của tầng phục vụ component LayoutRenderer (SVG) - Task T-502.
    /// Tự động ghép trạng thái realtime của ô đỗ và có cơ chế fallback tổng hợp khi chưa có bản vẽ.
    /// </summary>
    [HttpGet("floors/{floorId:int}/layout")]
    [ProducesResponseType<FloorLayoutDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FloorLayoutDto>> GetLayout(int floorId, CancellationToken cancellationToken)
    {
        var layout = await getFloorLayout.ExecuteAsync(floorId, cancellationToken);
        return Ok(layout);
    }
}
