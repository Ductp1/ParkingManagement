using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UserService.Application.Features.Identity;
using UserService.Application.Features.OwnerOnboarding;

namespace UserService.API.Controllers;

/// <summary>
/// US-116 U2 / U3 – AdminService cấp tài khoản chủ bãi sau khi thẩm định hồ sơ đối tác (gọi qua Gateway, chuyển tiếp JWT Admin).
/// Khóa / mở khóa / rời nền tảng của chủ bãi vẫn ở UsersController (cùng tiền tố owner-profiles, khác action).
/// </summary>
[ApiController]
[Route("api/v1/users/owner-profiles")]
[Authorize(Policy = "RequireAdmin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OwnerAccountsController(OwnerAccountUseCases owners, IGrantLotOwnerRoleUseCase grantLotOwner,
    UserLifecycleUseCases lifecycle, PartnerEmailUseCases partnerEmails) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/users/owner-profiles (header Idempotency-Key) – U2, một endpoint cho cả hai cách nộp hồ sơ đối tác:
    /// - có ApplicantUserId (người nộp đã đăng nhập – US-053): thêm role LotOwner + OwnerProfile cho chính tài khoản đó, không tạo
    ///   tài khoản mới, không cần link kích hoạt;
    /// - không có (đối tác nộp không cần tài khoản / Admin nhập – US-116): tạo User LotOwner + OwnerProfile, gửi link kích hoạt.
    /// 200 { userId, ownerProfileId }; gọi lại cho cùng hồ sơ trả đúng tài khoản; trùng / không cấp được → 409.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<IssuedOwnerAccount>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IssuedOwnerAccount>> Issue([FromBody] IssueOwnerAccountRequest request, CancellationToken ct)
    {
        if (request.ApplicantUserId is null) return Ok(await owners.IssueAsync(request, ct));

        var granted = await grantLotOwner.ExecuteAsync(new GrantLotOwnerRoleCommand(request.PartnerApplicationId, request.FullName,
            request.Email, request.Phone, request.BusinessName, request.TaxCode, request.BusinessAddress, request.BankBin, request.BankName,
            request.BankAccountNumber, request.BankAccountName, request.ApplicantUserId), ct);
        return Ok(new IssuedOwnerAccount(granted.UserId, granted.OwnerProfileId));
    }

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

    /// <summary>
    /// POST /api/v1/users/owner-profiles/partner-emails – AdminService nhờ gửi email cho người nộp hồ sơ (yêu cầu bổ sung / đã nhận bổ sung)
    /// qua outbox + SMTP của UserService. 202; SMTP chưa cấu hình → 503. Đặt chung controller vì cùng tiền tố và cùng quyền Admin.
    /// </summary>
    [HttpPost("partner-emails")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SendPartnerEmail([FromBody] PartnerEmailRequest request, CancellationToken ct)
    {
        await partnerEmails.EnqueueAsync(request, ct);
        return Accepted();
    }

    /// <summary>
    /// POST /api/v1/users/owner-profiles/5/discard { reason } – U6: hủy hồ sơ đối tác đã cấp tài khoản mà chủ bãi chưa kích hoạt.
    /// Thu hồi link, ẩn danh hóa tài khoản, nhả email / SĐT / mã số thuế. Idempotent; đã kích hoạt → 409; không có hồ sơ → 404.
    /// </summary>
    [HttpPost("{ownerProfileId:int}/discard")]
    [ProducesResponseType<UserLifecycleResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserLifecycleResult>> Discard(int ownerProfileId, [FromBody] DiscardOwnerAccountRequest request, CancellationToken ct)
        => Ok(await lifecycle.DiscardOwnerAccountAsync(ownerProfileId, request, ct));
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
