using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using PaymentService.Application.Features.Settlements;

namespace PaymentService.API.Controllers;

/// <summary>
/// Body tạo bút toán: amount ≠ 0 (VND, âm = trừ chủ bãi), reason bắt buộc (≤ 500); sanctionId tùy chọn (US-097);
/// reversesAdjustmentId = bút toán đã duyệt cần đảo (amount phải ngược dấu đúng bằng nó).
/// </summary>
public sealed record CreateAdjustmentRequest(int? OwnerProfileId, decimal? Amount, string? Reason, int? SanctionId, int? ReversesAdjustmentId);

/// <summary>Body duyệt: rowVersion (base64) của lần đọc trước.</summary>
public sealed record AdjustmentActionRequest(string? RowVersion);

/// <summary>Body từ chối: rowVersion + lý do.</summary>
public sealed record RejectAdjustmentRequest(string? RowVersion, string? Reason);

/// <summary>
/// US-080 (AC3): bút toán điều chỉnh tài chính – CHỈ THÊM, không sửa / xóa (đảo bằng bút toán ngược); tạo xong chờ một Admin khác duyệt,
/// bút toán đã duyệt được gắn vào kỳ Draft lần sinh / tính lại kế tiếp. Chỉ role Admin; người thao tác lấy từ JWT.
/// </summary>
[ApiController]
[Route("api/v1/payments/admin/adjustments")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminFinancialAdjustmentsController(ICurrentUser currentUser) : ControllerBase
{
    /// <summary>GET /api/v1/payments/admin/adjustments?ownerProfileId=2&amp;status=PendingApproval&amp;attached=false&amp;search=hoa%20hồng&amp;page=1&amp;pageSize=20</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<FinancialAdjustmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<FinancialAdjustmentDto>>> List([FromServices] IListAdjustmentsUseCase list,
        [FromQuery] int? ownerProfileId, [FromQuery] string? status, [FromQuery] bool? attached, [FromQuery] string? search,
        CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await list.ExecuteAsync(new AdjustmentListQuery(ownerProfileId, status, attached, search), page, pageSize, cancellationToken));

    /// <summary>POST /api/v1/payments/admin/adjustments – 201 bút toán PendingApproval; 400 thiếu / sai; 404 bút toán cần đảo không có; 409 đã đảo.</summary>
    [HttpPost]
    [ProducesResponseType<FinancialAdjustmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FinancialAdjustmentDto>> Create([FromServices] ICreateAdjustmentUseCase create,
        [FromBody] CreateAdjustmentRequest? request, CancellationToken cancellationToken)
    {
        var created = await create.ExecuteAsync(new CreateAdjustmentCommand(request?.OwnerProfileId, request?.Amount, request?.Reason,
            request?.SanctionId, request?.ReversesAdjustmentId), currentUser.UserId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>POST /api/v1/payments/admin/adjustments/5/approve { rowVersion } – 403 người tạo tự duyệt (4 mắt); 409 không còn chờ duyệt / dữ liệu đã đổi.</summary>
    [HttpPost("{id:int}/approve")]
    [ProducesResponseType<FinancialAdjustmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FinancialAdjustmentDto>> Approve([FromServices] IApproveAdjustmentUseCase approve, int id,
        [FromBody] AdjustmentActionRequest? request, CancellationToken cancellationToken)
        => Ok(await approve.ExecuteAsync(id, request?.RowVersion, currentUser.UserId, cancellationToken));

    /// <summary>POST /api/v1/payments/admin/adjustments/5/reject { rowVersion, reason }</summary>
    [HttpPost("{id:int}/reject")]
    [ProducesResponseType<FinancialAdjustmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FinancialAdjustmentDto>> Reject([FromServices] IRejectAdjustmentUseCase reject, int id,
        [FromBody] RejectAdjustmentRequest? request, CancellationToken cancellationToken)
        => Ok(await reject.ExecuteAsync(id, request?.RowVersion, request?.Reason, currentUser.UserId, cancellationToken));
}
