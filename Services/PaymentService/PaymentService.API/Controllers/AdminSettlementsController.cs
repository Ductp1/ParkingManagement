using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using PaymentService.Application.Features.Settlements;

namespace PaymentService.API.Controllers;

/// <summary>Body sinh kỳ: periodStart (yyyy-MM-dd, bỏ trống = kỳ gần nhất đã kết thúc), ownerProfileId (bỏ trống = mọi chủ bãi có phát sinh).</summary>
public sealed record GenerateSettlementsRequest(DateOnly? PeriodStart, int? OwnerProfileId);

/// <summary>Body tính lại: rowVersion (base64) đọc được ở lần tải kỳ trước.</summary>
public sealed record RegenerateSettlementRequest(string? RowVersion);

/// <summary>Body gửi duyệt / duyệt: rowVersion (base64) của lần đọc trước.</summary>
public sealed record SettlementActionRequest(string? RowVersion);

/// <summary>Body từ chối: rowVersion + lý do (bắt buộc, ≤ 500 ký tự).</summary>
public sealed record RejectSettlementRequest(string? RowVersion, string? Reason);

/// <summary>Body đã chi: rowVersion + mã giao dịch ngân hàng (bắt buộc, ≤ 100 ký tự) + thời điểm chuyển khoản (UTC).</summary>
public sealed record MarkSettlementPaidRequest(string? RowVersion, string? PayoutReference, DateTime? PaidAtUtc);

/// <summary>
/// US-080 (UC-43): Admin quản lý kỳ quyết toán của chủ bãi. Chỉ role Admin; người thao tác lấy từ JWT. Controller chỉ điều hướng HTTP → Use case.
/// Khoản giữ tranh chấp được đọc từ SupportService bằng chính JWT Admin của request.
/// </summary>
[ApiController]
[Route("api/v1/payments/admin/settlements")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminSettlementsController(IGenerateSettlementsUseCase generate, IRegenerateSettlementUseCase regenerate,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/payments/admin/settlements/generate – sinh kỳ Draft (hoặc tính lại Draft đã có) cho kỳ đã kết thúc.
    /// 200 { periodStart, periodEnd, cycle, owners: [ { ownerProfileId, outcome Created|Regenerated|Skipped, reason, settlementId, code,
    /// netPayout, isBlocked, blockedReasons, rowVersion } ] }; 400 kỳ không hợp lệ / chưa kết thúc; 409 đang có lượt sinh kỳ khác;
    /// 503 AdminService / BookingService / SupportService lỗi (không sinh gì, không đoán số).
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType<SettlementGenerationResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SettlementGenerationResultDto>> Generate([FromBody] GenerateSettlementsRequest? request,
        CancellationToken cancellationToken)
        => Ok(await generate.ExecuteAsync(new GenerateSettlementsCommand(request?.PeriodStart, request?.OwnerProfileId), currentUser.UserId,
            cancellationToken));

    /// <summary>
    /// POST /api/v1/payments/admin/settlements/12/regenerate – tính lại kỳ Draft (nạp khoản giữ, hoa hồng / booking / điều chỉnh mới nhất).
    /// 200 kết quả mới (kèm rowVersion mới); 400 thiếu rowVersion; 404 không có kỳ; 409 kỳ không còn Draft / dữ liệu đã đổi; 503 nguồn lỗi.
    /// </summary>
    [HttpPost("{id:int}/regenerate")]
    [ProducesResponseType<OwnerSettlementOutcomeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OwnerSettlementOutcomeDto>> Regenerate(int id, [FromBody] RegenerateSettlementRequest? request,
        CancellationToken cancellationToken)
        => Ok(await regenerate.ExecuteAsync(id, request?.RowVersion, currentUser.UserId, cancellationToken));

    // ===================== Đọc =====================

    /// <summary>
    /// GET /api/v1/payments/admin/settlements?status=PendingReview&amp;ownerProfileId=2&amp;periodStart=2026-11-02&amp;search=STL-2026&amp;blocked=true&amp;page=1&amp;pageSize=20
    /// – danh sách kỳ, mới nhất trước. 400 bộ lọc sai.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<SettlementSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<SettlementSummaryDto>>> List([FromServices] IListSettlementsUseCase list, [FromQuery] string? status,
        [FromQuery] int? ownerProfileId, [FromQuery] DateOnly? periodStart, [FromQuery] string? search, [FromQuery] bool? blocked,
        CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await list.ExecuteAsync(new SettlementListQuery(status, ownerProfileId, periodStart, search, blocked), page, pageSize, cancellationToken));

    /// <summary>GET /api/v1/payments/admin/settlements/stats?periodStart=2026-11-02 – số kỳ theo trạng thái, số kỳ bị chặn, thực chi chờ duyệt / chờ chuyển.</summary>
    [HttpGet("stats")]
    [ProducesResponseType<SettlementStatsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SettlementStatsDto>> Stats([FromServices] IGetSettlementStatsUseCase stats, [FromQuery] DateOnly? periodStart,
        CancellationToken cancellationToken)
        => Ok(await stats.ExecuteAsync(periodStart, cancellationToken));

    /// <summary>GET /api/v1/payments/admin/settlements/periods?take=26 – lịch kỳ gần nhất (hasEnded = sinh kỳ được).</summary>
    [HttpGet("periods")]
    [ProducesResponseType<IReadOnlyList<SettlementPeriodDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<SettlementPeriodDto>>> Periods([FromServices] IListSettlementPeriodsUseCase periods,
        CancellationToken cancellationToken, [FromQuery] int take = 26)
        => Ok(await periods.ExecuteAsync(take, cancellationToken));

    /// <summary>GET /api/v1/payments/admin/settlements/export?(cùng bộ lọc danh sách) – CSV, tối đa 10.000 kỳ.</summary>
    [HttpGet("export")]
    [Produces("text/csv", "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Export([FromServices] IExportSettlementsUseCase export, [FromQuery] string? status,
        [FromQuery] int? ownerProfileId, [FromQuery] DateOnly? periodStart, [FromQuery] string? search, [FromQuery] bool? blocked,
        CancellationToken cancellationToken)
    {
        var file = await export.ExecuteAsync(new SettlementListQuery(status, ownerProfileId, periodStart, search, blocked), cancellationToken);
        return File(file.Content, SettlementCsvFile.ContentType, file.FileName);
    }

    /// <summary>GET /api/v1/payments/admin/settlements/12 – chi tiết: công thức, mốc trạng thái, tổng theo loại dòng, bút toán đã gắn, thao tác được phép.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<SettlementDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SettlementDetailDto>> Get([FromServices] IGetSettlementUseCase get, int id, CancellationToken cancellationToken)
        => Ok(await get.ExecuteAsync(id, cancellationToken));

    /// <summary>GET /api/v1/payments/admin/settlements/12/lines?lineType=Refund&amp;page=1&amp;pageSize=50 – dòng chi tiết.</summary>
    [HttpGet("{id:int}/lines")]
    [ProducesResponseType<PagedResult<SettlementLineDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<SettlementLineDto>>> Lines([FromServices] IListSettlementLinesUseCase lines, int id,
        [FromQuery] string? lineType, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await lines.ExecuteAsync(id, lineType, page, pageSize, cancellationToken));

    /// <summary>GET /api/v1/payments/admin/settlements/12/lines/export – CSV dòng chi tiết của kỳ.</summary>
    [HttpGet("{id:int}/lines/export")]
    [Produces("text/csv", "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportLines([FromServices] IExportSettlementLinesUseCase export, int id, CancellationToken cancellationToken)
    {
        var file = await export.ExecuteAsync(id, cancellationToken);
        return File(file.Content, SettlementCsvFile.ContentType, file.FileName);
    }

    // ===================== Chuyển trạng thái (409 sai trạng thái / dữ liệu đã đổi; 403 tự duyệt khi bật 4 mắt) =====================

    /// <summary>POST /api/v1/payments/admin/settlements/12/submit { rowVersion } – Draft → PendingReview (kỳ bị chặn → 409).</summary>
    [HttpPost("{id:int}/submit")]
    [ProducesResponseType<SettlementSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SettlementSummaryDto>> Submit([FromServices] ISubmitSettlementUseCase submit, int id,
        [FromBody] SettlementActionRequest? request, CancellationToken cancellationToken)
        => Ok(await submit.ExecuteAsync(id, request?.RowVersion, currentUser.UserId, cancellationToken));

    /// <summary>POST /api/v1/payments/admin/settlements/12/approve { rowVersion } – PendingReview → Approved (chốt kỳ).</summary>
    [HttpPost("{id:int}/approve")]
    [ProducesResponseType<SettlementSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SettlementSummaryDto>> Approve([FromServices] IApproveSettlementUseCase approve, int id,
        [FromBody] SettlementActionRequest? request, CancellationToken cancellationToken)
        => Ok(await approve.ExecuteAsync(id, request?.RowVersion, currentUser.UserId, cancellationToken));

    /// <summary>POST /api/v1/payments/admin/settlements/12/reject { rowVersion, reason } – PendingReview → Draft.</summary>
    [HttpPost("{id:int}/reject")]
    [ProducesResponseType<SettlementSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SettlementSummaryDto>> Reject([FromServices] IRejectSettlementUseCase reject, int id,
        [FromBody] RejectSettlementRequest? request, CancellationToken cancellationToken)
        => Ok(await reject.ExecuteAsync(id, request?.RowVersion, request?.Reason, currentUser.UserId, cancellationToken));

    /// <summary>POST /api/v1/payments/admin/settlements/12/mark-paid { rowVersion, payoutReference, paidAtUtc } – Approved → Paid.</summary>
    [HttpPost("{id:int}/mark-paid")]
    [ProducesResponseType<SettlementSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SettlementSummaryDto>> MarkPaid([FromServices] IMarkSettlementPaidUseCase markPaid, int id,
        [FromBody] MarkSettlementPaidRequest? request, CancellationToken cancellationToken)
        => Ok(await markPaid.ExecuteAsync(id, request?.RowVersion, request?.PayoutReference, request?.PaidAtUtc, currentUser.UserId,
            cancellationToken));
}
