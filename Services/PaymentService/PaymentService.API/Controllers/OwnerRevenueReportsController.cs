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
public sealed class OwnerRevenueReportsController(IGetOwnerRevenueSummaryUseCase getSummary, IGetOwnerRevenueSeriesUseCase getSeries,
    IListOwnerRevenueTransactionsUseCase listTransactions, IExportOwnerRevenueTransactionsUseCase exportTransactions,
    ICurrentUser currentUser) : ControllerBase
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

    /// <summary>
    /// GET /api/v1/payments/reports/owner-revenue/series?from=2026-10-01&amp;to=2026-11-01&amp;parkingLotId=1&amp;granularity=Week
    /// granularity: Hour (khoảng ≤ 7 ngày) | Day (mặc định) | Week (bắt đầu thứ Hai) | Month. Đủ mọi điểm, kể cả điểm bằng 0.
    /// </summary>
    [HttpGet("series")]
    [ProducesResponseType<OwnerRevenueSeriesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OwnerRevenueSeriesDto>> Series([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int? parkingLotId, [FromQuery] string? granularity, CancellationToken cancellationToken)
        => Ok(await getSeries.ExecuteAsync(currentUser.OwnerProfileId, new OwnerRevenueQuery(from, to, parkingLotId), granularity,
            cancellationToken));

    /// <summary>
    /// GET /api/v1/payments/reports/owner-revenue/transactions?from=2026-10-01&amp;to=2026-11-01&amp;parkingLotId=1&amp;method=Vnpay
    /// &amp;purpose=Booking&amp;status=PartiallyRefunded&amp;page=1&amp;pageSize=20 – mới nhất trước; pageSize tối đa 100.
    /// </summary>
    [HttpGet("transactions")]
    [ProducesResponseType<PagedResult<OwnerRevenueTransactionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<OwnerRevenueTransactionDto>>> Transactions([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int? parkingLotId, [FromQuery] string? method, [FromQuery] string? purpose, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await listTransactions.ExecuteAsync(currentUser.OwnerProfileId,
            new OwnerRevenueTransactionQuery(from, to, parkingLotId, method, purpose, status), page, pageSize, cancellationToken));

    /// <summary>GET /api/v1/payments/reports/owner-revenue/export?(cùng tham số lọc của danh sách) – file CSV, tối đa 10.000 dòng.</summary>
    [HttpGet("export")]
    [Produces("text/csv", "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Export([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? parkingLotId,
        [FromQuery] string? method, [FromQuery] string? purpose, [FromQuery] string? status, CancellationToken cancellationToken)
    {
        var file = await exportTransactions.ExecuteAsync(currentUser.OwnerProfileId,
            new OwnerRevenueTransactionQuery(from, to, parkingLotId, method, purpose, status), cancellationToken);
        return File(file.Content, OwnerRevenueCsvFile.ContentType, file.FileName);
    }
}
