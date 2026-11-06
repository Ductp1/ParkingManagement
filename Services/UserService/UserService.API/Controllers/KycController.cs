using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ParkingManagement.SharedKernel.Enums;
using UserService.Application.Features.Kyc;

namespace UserService.API.Controllers;

/// <summary>US-005: người dùng gửi CCCD/GPLX để định danh.</summary>
[ApiController]
[Authorize]
[Route("api/v1/users/me/kyc")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class KycController(KycUseCases kyc) : ControllerBase
{
    private const long RequestLimit = 2L * KycUseCases.MaxImageBytes + 65536;

    [HttpGet]
    public async Task<ActionResult<KycStatusDto>> Mine(CancellationToken ct) => Ok(await kyc.GetMineAsync(ct));

    [HttpPost]
    [EnableRateLimiting("auth")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimit)]
    public async Task<ActionResult<KycStatusDto>> Submit([FromForm] SubmitKycForm form, CancellationToken ct)
    {
        await using var front = form.FrontImage.OpenReadStream();
        await using var back = form.BackImage.OpenReadStream();
        var result = await kyc.SubmitAsync(new SubmitKycCommand(form.DocumentType, form.DocumentNumber,
            new KycImageUpload(front, form.FrontImage.Length), new KycImageUpload(back, form.BackImage.Length)), ct);
        return Accepted(result);
    }

    /// <summary>POST /api/v1/users/me/kyc/citizen-id — xác minh chỉ bằng số CCCD 12 chữ số (không cần ảnh).</summary>
    [HttpPost("citizen-id")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<KycStatusDto>> SubmitCitizenId(SubmitCitizenIdBody body, CancellationToken ct)
        => Accepted(await kyc.SubmitCitizenIdAsync(body.DocumentNumber, ct));

    /// <summary>Ảnh giấy tờ của chính mình, chỉ trả qua phiên đăng nhập (không có URL công khai).</summary>
    [HttpGet("images/{side}")]
    public async Task<IActionResult> Image(KycImageSide side, CancellationToken ct)
    {
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(await kyc.ReadMyImageAsync(side, ct), "image/jpeg");
    }
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

    [HttpGet("{userId:int}/images/{side}")]
    public async Task<IActionResult> Image(int userId, KycImageSide side, CancellationToken ct)
    {
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(await kyc.ReadImageForReviewAsync(userId, side, ct), "image/jpeg");
    }

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

public sealed class SubmitKycForm
{
    [Required] public KycDocumentType DocumentType { get; set; }
    [Required, MaxLength(32)] public string DocumentNumber { get; set; } = "";
    [Required] public IFormFile FrontImage { get; set; } = null!;
    [Required] public IFormFile BackImage { get; set; } = null!;
}
public sealed record RejectKycBody([MaxLength(500)] string? Reason);
public sealed record SubmitCitizenIdBody([Required, MaxLength(32)] string DocumentNumber);
