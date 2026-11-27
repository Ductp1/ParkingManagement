using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UserService.Application.Features.Kyc;

namespace UserService.API.Controllers;

/// <summary>US-005: người dùng xác minh danh tính bằng số CCCD (giả lập liên kết VNeID).</summary>
[ApiController]
[Authorize]
[Route("api/v1/users/me/kyc")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class KycController(KycUseCases kyc) : ControllerBase
{
    /// <summary>GET /api/v1/users/me/kyc — trạng thái xác minh của chính mình (số CCCD đã che).</summary>
    [HttpGet]
    public async Task<ActionResult<KycStatusDto>> Mine(CancellationToken ct) => Ok(await kyc.GetMineAsync(ct));

    /// <summary>POST /api/v1/users/me/kyc/citizen-id — xác minh chỉ bằng số CCCD 12 chữ số (không cần ảnh).</summary>
    [HttpPost("citizen-id")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<KycStatusDto>> SubmitCitizenId(SubmitCitizenIdBody body, CancellationToken ct)
        => Accepted(await kyc.SubmitCitizenIdAsync(body.DocumentNumber, ct));
}

/// <summary>US-005: Admin duyệt hoặc từ chối hồ sơ định danh.</summary>
[ApiController]
[Authorize(Policy = "RequireAdmin")]
[Route("api/v1/users/kyc")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminKycController(KycUseCases kyc) : ControllerBase
{
    /// <summary>GET /api/v1/users/kyc?page=1 — hồ sơ chờ duyệt, gửi sớm nhất lên trước.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminKycItem>>> Pending([FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await kyc.ListPendingAsync(page, ct));

    [HttpPost("{userId:int}/approve")]
    public async Task<IActionResult> Approve(int userId, CancellationToken ct)
    {
        await kyc.ApproveAsync(userId, ct);
        return NoContent();
    }

    [HttpPost("{userId:int}/reject")]
    public async Task<IActionResult> Reject(int userId, RejectKycBody body, CancellationToken ct)
    {
        await kyc.RejectAsync(userId, new ReviewKyc(body.Reason), ct);
        return NoContent();
    }
}

public sealed record RejectKycBody([MaxLength(500)] string? Reason);
public sealed record SubmitCitizenIdBody([Required, MaxLength(32)] string DocumentNumber);
