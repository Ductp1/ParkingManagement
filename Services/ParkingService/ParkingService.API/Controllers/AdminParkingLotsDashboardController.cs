using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingService.Application.Features.AdminDashboard;

namespace ParkingService.API.Controllers;

/// <summary>
/// US-081 (UC-45): danh sách bãi cho Admin Dashboard. Route nằm dưới "parking-lots" để Gateway chuyển sang ParkingService;
/// "admin/lots-summary" không trùng route "{id:int}" có sẵn. Chỉ đọc, chỉ role Admin.
/// </summary>
[ApiController]
[Route("api/v1/parking-lots/admin")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminParkingLotsDashboardController(IGetAdminLotsSummaryUseCase getLotsSummary) : ControllerBase
{
    /// <summary>GET /api/v1/parking-lots/admin/lots-summary – bãi chưa xóa: tên, quận, trạng thái, tổng chỗ, cờ vận hành.</summary>
    [HttpGet("lots-summary")]
    [ProducesResponseType<AdminLotsSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminLotsSummaryDto>> LotsSummary(CancellationToken cancellationToken)
        => Ok(await getLotsSummary.ExecuteAsync(cancellationToken));
}
