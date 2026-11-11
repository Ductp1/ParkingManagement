using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using PaymentService.Application.Features.Settlements;

namespace PaymentService.API.Controllers;

/// <summary>
/// US-080 (UC-35 bước 3): chủ bãi đang đăng nhập xem các kỳ quyết toán đã chốt (Approved / Paid) của mình. Chỉ role LotOwner;
/// OwnerProfileId luôn lấy từ token (không nhận từ query) – kỳ của chủ bãi khác → 404. Không có dữ liệu cá nhân của khách.
/// </summary>
[ApiController]
[Route("api/v1/payments/owner/settlements")]
[Authorize(Policy = "RequireOwner")]
public sealed class OwnerSettlementsController(ICurrentUser currentUser) : ControllerBase
{
    /// <summary>GET /api/v1/payments/owner/settlements?page=1&amp;pageSize=20 – kỳ đã chốt, mới nhất trước.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<OwnerSettlementDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<OwnerSettlementDto>>> List([FromServices] IListOwnerSettlementsUseCase list,
        CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await list.ExecuteAsync(currentUser.OwnerProfileId, page, pageSize, cancellationToken));

    /// <summary>GET /api/v1/payments/owner/settlements/12 – công thức, tổng theo loại dòng, điều chỉnh đã gắn.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<OwnerSettlementDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OwnerSettlementDetailDto>> Get([FromServices] IGetOwnerSettlementUseCase get, int id,
        CancellationToken cancellationToken)
        => Ok(await get.ExecuteAsync(currentUser.OwnerProfileId, id, cancellationToken));

    /// <summary>GET /api/v1/payments/owner/settlements/12/lines?lineType=Booking&amp;page=1&amp;pageSize=50</summary>
    [HttpGet("{id:int}/lines")]
    [ProducesResponseType<PagedResult<SettlementLineDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<SettlementLineDto>>> Lines([FromServices] IListOwnerSettlementLinesUseCase lines, int id,
        [FromQuery] string? lineType, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await lines.ExecuteAsync(currentUser.OwnerProfileId, id, lineType, page, pageSize, cancellationToken));

    /// <summary>GET /api/v1/payments/owner/settlements/12/export – CSV dòng chi tiết để đối soát.</summary>
    [HttpGet("{id:int}/export")]
    [Produces("text/csv", "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Export([FromServices] IExportOwnerSettlementLinesUseCase export, int id, CancellationToken cancellationToken)
    {
        var file = await export.ExecuteAsync(currentUser.OwnerProfileId, id, cancellationToken);
        return File(file.Content, SettlementCsvFile.ContentType, file.FileName);
    }
}
