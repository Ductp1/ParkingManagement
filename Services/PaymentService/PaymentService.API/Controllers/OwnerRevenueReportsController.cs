using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using PaymentService.Application.Features.Reports;

namespace PaymentService.API.Controllers;

/// <summary>
/// UC-35 / US-078: báo cáo doanh thu cho chủ bãi đang đăng nhập. Chỉ role LotOwner; OwnerProfileId luôn lấy từ token,
/// không nhận từ query (multi-tenant isolation – AC2). Chỉ đọc: số liệu không sửa được qua API (AC4).
/// Controller chỉ điều hướng HTTP → Use case.
/// </summary>
[ApiController]
[Route("api/v1/payments/reports/owner-revenue")]
[Authorize(Policy = "RequireOwner")]
public sealed class OwnerRevenueReportsController(IGetOwnerRevenueSummaryUseCase getSummary, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/payments/reports/owner-revenue/summary?from=2026-10-01&amp;to=2026-11-01&amp;parkingLotId=1
    /// from/to là ngày theo giờ Việt Nam (from bao gồm, to loại trừ); bỏ trống cả hai → tháng hiện tại. Tối đa 366 ngày.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType<OwnerRevenueSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OwnerRevenueSummaryDto>> Summary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int? parkingLotId, CancellationToken cancellationToken)
        => Ok(await getSummary.ExecuteAsync(currentUser.OwnerProfileId, new OwnerRevenueQuery(from, to, parkingLotId), cancellationToken));
}
