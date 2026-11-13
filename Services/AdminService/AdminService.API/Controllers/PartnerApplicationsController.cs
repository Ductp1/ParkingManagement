using AdminService.API.Security;
using AdminService.Application.Features.PartnerOnboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AdminService.API.Controllers;

/// <summary>
/// US-116 T1 / US-053 (P1): form public "Đăng ký đối tác" – chủ bãi nộp hồ sơ, KHÔNG cần đăng nhập, không tạo tài khoản.
/// Admin thẩm định ở api/v1/admin/partner-applications rồi mới cấp tài khoản. Giới hạn số lần gửi theo IP (policy "partner-apply").
/// </summary>
[ApiController]
[Route("api/v1/partner-applications")]
[AllowAnonymous]
public sealed class PartnerApplicationsController(ISubmitPartnerApplicationUseCase submit, IGetPartnerApplicationForEditUseCase getForEdit,
    IResubmitPartnerApplicationUseCase resubmit) : ControllerBase
{
    public const string EditTokenHeader = "X-Edit-Token";

    /// <summary>POST /api/v1/partner-applications – 201 { code, status, submittedAtUtc }.</summary>
    [HttpPost]
    [EnableRateLimiting(PartnerApplicationRateLimit.Policy)]
    [ProducesResponseType<PartnerApplicationReceiptDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PartnerApplicationReceiptDto>> Submit([FromBody] SubmitPartnerApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var receipt = await submit.ExecuteAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, receipt);
    }

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
