using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.Features.Auth;
using UserService.Application.Features.Identity;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using Microsoft.AspNetCore.RateLimiting;

namespace UserService.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public sealed class AuthController(ILoginUseCase loginUseCase, IRefreshSessionUseCase refreshUseCase, ILogoutUseCase logoutUseCase,
    AccountUseCases accounts, ICurrentUser current) : ControllerBase
{
    /// <summary>POST /api/v1/auth/login -> Đăng nhập bằng Email/SĐT (US-003).</summary>
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponseDto>> Login(
        [FromBody] LoginDto request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        try
        {
            return Ok(await loginUseCase.ExecuteAsync(request, cancellationToken));
        }
        catch (UserService.Application.Interfaces.CaptchaException ex)
        {
            var problem = new ProblemDetails
            {
                Status = ex.Unavailable ? 503 : 400,
                Title = ex.Unavailable ? "Dịch vụ CAPTCHA chưa sẵn sàng" : "CAPTCHA không hợp lệ",
                Detail = ex.Message, Instance = Request.Path
            };
            problem.Extensions["code"] = ex.Code;
            return StatusCode(problem.Status.Value, problem);
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponseDto>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        return Ok(await refreshUseCase.ExecuteAsync(request, cancellationToken));
    }

    // Refresh token là bằng chứng sở hữu phiên; logout vẫn hoạt động khi access token đã hết hạn.
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        await logoutUseCase.ExecuteAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        await accounts.RegisterAsync(request, ct);
        return Accepted(new { Message = "Nếu định danh hợp lệ và chưa được sử dụng, mã xác minh sẽ được gửi." });
    }
    [HttpPost("otp/verify")]
    public async Task<IActionResult> Verify(VerifyOtpRequest request, CancellationToken ct)
    {
        await accounts.VerifyRegistrationAsync(request, ct); return NoContent();
    }
    [HttpPost("otp/resend")]
    public async Task<IActionResult> Resend(OtpRequest request, CancellationToken ct)
    {
        await accounts.RequestOtpAsync(request, ct); return Accepted(new { Message = "Nếu tài khoản đủ điều kiện, mã xác minh sẽ được gửi." });
    }
    [HttpGet("session")]
    [DisableRateLimiting]
    public IActionResult Session()
    {
        // AllowAnonymous của controller được giữ để login/refresh hoạt động; tự kiểm tra ở endpoint này.
        if (!current.IsAuthenticated) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(new { current.UserId, current.Roles, current.OwnerProfileId, current.ParkingLotIds });
    }

}
