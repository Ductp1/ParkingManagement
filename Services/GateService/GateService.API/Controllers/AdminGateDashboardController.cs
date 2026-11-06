using GateService.Application.Features.AdminDashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GateService.API.Controllers;

/// <summary>
/// US-081 (UC-45): số liệu tổng cho Admin Dashboard (AdminService gọi qua Gateway, prefix /api/v1/gate đã được định tuyến).
/// Chỉ đọc, chỉ role Admin; không trả biển số, người dùng hay nhân viên. Controller chỉ điều hướng HTTP → Use case.
/// </summary>
[ApiController]
[Route("api/v1/gate/admin")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminGateDashboardController(IGetAdminGateOccupancyUseCase getOccupancy, IGetAdminGateDevicesUseCase getDevices)
    : ControllerBase
{
    /// <summary>GET /api/v1/gate/admin/occupancy – phiên đang hoạt động toàn sàn + theo bãi (kèm Capacity US-066 nếu có).</summary>
    [HttpGet("occupancy")]
    [ProducesResponseType<AdminGateOccupancyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminGateOccupancyDto>> Occupancy(CancellationToken cancellationToken)
        => Ok(await getOccupancy.ExecuteAsync(cancellationToken));

    /// <summary>GET /api/v1/gate/admin/devices – thiết bị cổng đang dùng theo loại × trạng thái.</summary>
    [HttpGet("devices")]
    [ProducesResponseType<AdminGateDevicesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminGateDevicesDto>> Devices(CancellationToken cancellationToken)
        => Ok(await getDevices.ExecuteAsync(cancellationToken));
}
