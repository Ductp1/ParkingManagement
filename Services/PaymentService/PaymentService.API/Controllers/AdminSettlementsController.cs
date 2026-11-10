using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using PaymentService.Application.Features.Settlements;

namespace PaymentService.API.Controllers;

/// <summary>Body sinh kỳ: periodStart (yyyy-MM-dd, bỏ trống = kỳ gần nhất đã kết thúc), ownerProfileId (bỏ trống = mọi chủ bãi có phát sinh).</summary>
public sealed record GenerateSettlementsRequest(DateOnly? PeriodStart, int? OwnerProfileId);

/// <summary>Body tính lại: rowVersion (base64) đọc được ở lần tải kỳ trước.</summary>
public sealed record RegenerateSettlementRequest(string? RowVersion);

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
}
