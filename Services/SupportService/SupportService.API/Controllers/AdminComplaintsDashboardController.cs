using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportService.Application.Features.AdminDashboard;

namespace SupportService.API.Controllers;

/// <summary>
/// US-081 (UC-45): số liệu tranh chấp cho Admin Dashboard (AdminService gọi qua Gateway, prefix /api/v1/complaints đã được định tuyến).
/// Route 2 đoạn "admin/summary" nên không trùng GET /api/v1/complaints/{code}. Chỉ đọc, chỉ role Admin.
/// </summary>
[ApiController]
[Route("api/v1/complaints/admin")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminComplaintsDashboardController(IGetAdminComplaintSummaryUseCase getSummary) : ControllerBase
{
    /// <summary>GET /api/v1/complaints/admin/summary – tranh chấp chưa đóng theo trạng thái + số chờ chủ bãi đã quá hạn 48 giờ.</summary>
    [HttpGet("summary")]
    [ProducesResponseType<AdminComplaintSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminComplaintSummaryDto>> Summary(CancellationToken cancellationToken)
        => Ok(await getSummary.ExecuteAsync(cancellationToken));
}
