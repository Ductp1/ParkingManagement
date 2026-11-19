using AdminService.API.Security;
using AdminService.Application.Features.PartnerOnboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AdminService.API.Controllers;

/// <summary>
/// US-116 T4: link bổ sung hồ sơ đối tác (không đăng nhập, xác thực bằng header X-Edit-Token). Nộp hồ sơ mới đi qua
/// POST api/v1/admin/partner-registrations (multipart, kèm giấy tờ). Giới hạn số lần mở / gửi theo IP (policy "partner-edit").
/// </summary>
[ApiController]
[Route("api/v1/partner-applications")]
[AllowAnonymous]
public sealed class PartnerApplicationsController(IGetPartnerApplicationForEditUseCase getForEdit,
    IResubmitPartnerApplicationUseCase resubmit) : ControllerBase
{
    public const string EditTokenHeader = "X-Edit-Token";

    /// <summary>
    /// GET /api/v1/partner-applications/PA-2026-0012 (header X-Edit-Token) – P2 / T4: đối tác mở link bổ sung, nhận dữ liệu form + nội dung
    /// Admin yêu cầu. Mã / token sai, hết hạn, hồ sơ không còn chờ bổ sung → 400 cùng một thông báo.
    /// </summary>
    [HttpGet("{code}")]
    [EnableRateLimiting(PartnerApplicationRateLimit.EditPolicy)]
    [ProducesResponseType<PartnerApplicationEditDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PartnerApplicationEditDto>> GetForEdit(string code, CancellationToken cancellationToken)
        => Ok(await getForEdit.ExecuteAsync(code, Request.Headers[EditTokenHeader].ToString(), cancellationToken));

    /// <summary>PUT /api/v1/partner-applications/PA-2026-0012/resubmit (header X-Edit-Token) – P3 / T4: gửi bổ sung → UnderReview.</summary>
    [HttpPut("{code}/resubmit")]
    [EnableRateLimiting(PartnerApplicationRateLimit.EditPolicy)]
    [ProducesResponseType<PartnerApplicationReceiptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PartnerApplicationReceiptDto>> Resubmit(string code, [FromBody] SubmitPartnerApplicationRequest request,
        CancellationToken cancellationToken)
        => Ok(await resubmit.ExecuteAsync(code, Request.Headers[EditTokenHeader].ToString(), request, cancellationToken));
}
