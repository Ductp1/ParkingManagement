using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Features.Commission;

namespace PaymentService.API.Controllers;

/// <summary>US-079: hoa hồng nền tảng toàn sàn cho Admin portal (Tài chính → Hoa hồng). Chỉ role Admin. Controller chỉ điều hướng HTTP → Use case.</summary>
[ApiController]
[Route("api/v1/payments/admin")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminCommissionController(IGetAdminCommissionSummaryUseCase getSummary) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/payments/admin/commission-summary?fromUtc=2026-10-31T17:00:00Z&amp;toUtc=2026-11-30T17:00:00Z – theo PaidAtUtc của giao dịch
    /// gốc, tối đa 366 ngày: tổng GMV / hoàn / thuần / hoa hồng, theo bãi, chủ bãi, nguồn tỷ lệ và từng ngày (giờ Việt Nam).
    /// Chỉ số tổng hợp – không có dữ liệu từng khách. pendingCount &gt; 0 = còn giao dịch chờ job tính.
    /// </summary>
    [HttpGet("commission-summary")]
    [ProducesResponseType<AdminCommissionSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminCommissionSummaryDto>> CommissionSummary([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
        => Ok(await getSummary.ExecuteAsync(fromUtc, toUtc, cancellationToken));
}
