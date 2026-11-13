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
public sealed class PartnerApplicationsController(ISubmitPartnerApplicationUseCase submit) : ControllerBase
{
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
}
