using AdminService.Application.Features.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminService.API.Controllers;

/// <summary>US-081 (UC-45): Admin Dashboard – tổng quan toàn sàn. Controller chỉ điều hướng HTTP → Use case. Chỉ role Admin.</summary>
[ApiController]
[Route("api/v1/admin/dashboard")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminDashboardController(IGetDashboardOverviewUseCase getOverview) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/admin/dashboard/overview – mỗi widget có status available (kèm value, asOfUtc) hoặc unavailable
    /// (reason no_source / source_error). Một nguồn lỗi không làm hỏng response (vẫn 200).
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType<DashboardOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardOverviewDto>> Overview(CancellationToken cancellationToken)
        => Ok(await getOverview.ExecuteAsync(cancellationToken));
}
