using AdminService.Application.Features.Commission;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminService.API.Controllers;

/// <summary>US-079: tỷ lệ hoa hồng nền tảng của từng bãi cho trang Tài chính → Hoa hồng. Controller chỉ điều hướng HTTP → Use case. Chỉ role Admin.</summary>
[ApiController]
[Route("api/v1/admin/commission-rates")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminCommissionRatesController(IListCommissionRatesUseCase listRates) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/admin/commission-rates?search=&amp;source=LotContract|DefaultConfig&amp;page=1&amp;pageSize=20 – tỷ lệ hiện tại (phân số),
    /// nguồn, lý do, hợp đồng của từng bãi + tỷ lệ mặc định và thay đổi đã lên lịch. ParkingService lỗi → vẫn 200, partial = true.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<CommissionRateListDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CommissionRateListDto>> List([FromQuery] string? search, [FromQuery] string? source,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await listRates.ExecuteAsync(new CommissionRateListQuery(search, source, page, pageSize), cancellationToken));
}
