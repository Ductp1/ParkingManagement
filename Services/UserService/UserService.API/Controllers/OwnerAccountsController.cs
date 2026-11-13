using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UserService.Application.Features.Identity;

namespace UserService.API.Controllers;

/// <summary>
/// US-116 U2 / U3 – AdminService cấp tài khoản chủ bãi sau khi thẩm định hồ sơ đối tác (gọi qua Gateway, chuyển tiếp JWT Admin).
/// Khóa / mở khóa / rời nền tảng của chủ bãi vẫn ở UsersController (cùng tiền tố owner-profiles, khác action).
/// </summary>
[ApiController]
[Route("api/v1/users/owner-profiles")]
[Authorize(Policy = "RequireAdmin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OwnerAccountsController(OwnerAccountUseCases owners) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/users/owner-profiles (header Idempotency-Key) – tạo User LotOwner + OwnerProfile, gửi link kích hoạt.
    /// 200 { userId, ownerProfileId }; gọi lại cho cùng hồ sơ trả đúng tài khoản đã tạo; trùng email / SĐT / MST → 409.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<IssuedOwnerAccount>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IssuedOwnerAccount>> Issue([FromBody] IssueOwnerAccountRequest request, CancellationToken ct)
        => Ok(await owners.IssueAsync(request, ct));

    /// <summary>POST /api/v1/users/owner-profiles/5/activation-link – thu hồi link cũ, gửi link mới. 204; đã kích hoạt → 409.</summary>
    [HttpPost("{ownerProfileId:int}/activation-link")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResendActivation(int ownerProfileId, CancellationToken ct)
    {
        await owners.ResendActivationAsync(ownerProfileId, ct);
        return NoContent();
    }
}

/// <summary>
/// US-116 U4 / U5 – chủ bãi mở link trong email và tự đặt mật khẩu (không cần đăng nhập). Token đi trong header / body, không ở query
/// string nên không nằm trong log truy cập. Giới hạn tần suất chung với các API xác thực (policy "auth").
/// </summary>
[ApiController]
[Route("api/v1/auth/owner-activation")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OwnerActivationController(OwnerAccountUseCases owners) : ControllerBase
{
    public const string TokenHeader = "X-Activation-Token";

    /// <summary>GET /api/v1/auth/owner-activation (header X-Activation-Token) – 200 { fullName, email, businessName, expiresAtUtc }; link hỏng → 400.</summary>
    [HttpGet]
    [ProducesResponseType<OwnerActivationInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OwnerActivationInfo>> Validate(CancellationToken ct)
        => Ok(await owners.ValidateActivationAsync(Request.Headers[TokenHeader].ToString(), ct));

    /// <summary>POST /api/v1/auth/owner-activation { token, password } – 204; tài khoản Active, link hết hiệu lực.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Activate([FromBody] ActivateOwnerRequest request, CancellationToken ct)
    {
        await owners.ActivateAsync(request, ct);
        return NoContent();
    }
}
