using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Application.Features.Kyb;

namespace ParkingService.API.Controllers;

public sealed record SaveKybDraftRequest(
    int OwnerProfileId,
    string? BusinessLicenseUrl = null,
    IReadOnlyList<string>? SitePhotoUrls = null,
    double? PhotoLatitude = null,
    double? PhotoLongitude = null,
    string? FireSafetyCertificateUrl = null,
    DateTime? FieldSurveyAtUtc = null,
    string? FieldSurveyNote = null);

public sealed record SubmitKybApplicationRequest(
    int OwnerProfileId,
    string BusinessLicenseUrl,
    IReadOnlyList<string> SitePhotoUrls,
    double? PhotoLatitude,
    double? PhotoLongitude,
    string FireSafetyCertificateUrl,
    DateTime? FieldSurveyAtUtc,
    string? FieldSurveyNote);

/// <summary>
/// Controller quản lý hồ sơ thẩm định KYB 4 bước của bãi đỗ xe (US-054).
/// Quy trình: Bước 1 Pháp lý -> Bước 2 Địa điểm & GPS -> Bước 3 PCCC -> Bước 4 Khảo sát thực địa.
/// Yêu cầu xác thực JWT Bearer để bảo vệ thông tin pháp lý doanh nghiệp, chống lỗ hổng IDOR.
/// </summary>
[ApiController]
[Route("api/v1/parking-lots/{lotId:int}/kyb")]
[Authorize]
public sealed class KybApplicationsController(
    IGetKybApplicationUseCase getKybApplication,
    ISaveKybDraftUseCase saveKybDraft,
    ISubmitKybApplicationUseCase submitKybApplication,
    IUploadKybDocumentUseCase uploadDocument,
    IGetKybDocumentFileUseCase getDocumentFile,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>GET /api/v1/parking-lots/{lotId}/kyb – Lấy thông tin hồ sơ KYB (Read-only, bảo mật: chỉ Chủ bãi sở hữu hoặc Admin).</summary>
    [HttpGet]
    [ProducesResponseType<KybApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KybApplicationDto>> GetKybApplication(
        int lotId,
        [FromQuery] int? ownerProfileId,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerId = currentUser.OwnerProfileId ?? ownerProfileId ?? 0;
        var query = new GetKybApplicationQuery(lotId, effectiveOwnerId);
        return Ok(await getKybApplication.ExecuteAsync(query, cancellationToken));
    }

    /// <summary>PUT /api/v1/parking-lots/{lotId}/kyb/draft – Lưu nháp tiến độ hồ sơ KYB (Upsert: Cập nhật nếu đã có, tạo mới nếu chưa; không sinh duplicate bản ghi).</summary>
    [HttpPut("draft")]
    [ProducesResponseType<KybApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KybApplicationDto>> SaveDraft(
        int lotId,
        [FromBody] SaveKybDraftRequest request,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerId = currentUser.OwnerProfileId ?? request.OwnerProfileId;
        var command = new SaveKybDraftCommand(
            lotId,
            effectiveOwnerId,
            request.BusinessLicenseUrl,
            request.SitePhotoUrls,
            request.PhotoLatitude,
            request.PhotoLongitude,
            request.FireSafetyCertificateUrl,
            request.FieldSurveyAtUtc,
            request.FieldSurveyNote);

        return Ok(await saveKybDraft.ExecuteAsync(command, cancellationToken));
    }

    /// <summary>POST /api/v1/parking-lots/{lotId}/kyb/submit – Nộp chính thức hồ sơ KYB (Upsert: Cập nhật bản ghi Draft/Rejected hiện có sang Submitted; không sinh duplicate bản ghi).</summary>
    [HttpPost("submit")]
    [AllowAnonymous]
    [ProducesResponseType<KybApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KybApplicationDto>> Submit(
        int lotId,
        [FromBody] SubmitKybApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerId = currentUser.OwnerProfileId ?? request.OwnerProfileId;
        var command = new SubmitKybApplicationCommand(
            lotId,
            effectiveOwnerId,
            request.BusinessLicenseUrl,
            request.SitePhotoUrls,
            request.PhotoLatitude,
            request.PhotoLongitude,
            request.FireSafetyCertificateUrl,
            request.FieldSurveyAtUtc,
            request.FieldSurveyNote);

        return Ok(await submitKybApplication.ExecuteAsync(command, cancellationToken));
    }

    /// <summary>POST /api/v1/parking-lots/{lotId}/kyb/documents/upload – Tải lên file tài liệu thật (PDF/ảnh ĐKKD, PCCC, hiện trường) (US-054).</summary>
    [HttpPost("documents/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType<UploadKybDocumentResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UploadKybDocumentResultDto>> UploadDocument(
        int lotId,
        IFormFile file,
        [FromQuery] KybDocumentType documentType,
        [FromQuery] int? ownerProfileId,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ProblemDetails { Title = "Dữ liệu không hợp lệ", Detail = "File tài liệu không được để trống.", Status = 400 });

        var effectiveOwnerId = currentUser.OwnerProfileId ?? ownerProfileId;
        var isAdmin = currentUser.Roles.Contains("Admin");

        await using var stream = file.OpenReadStream();
        var result = await uploadDocument.ExecuteAsync(
            lotId,
            documentType,
            file.FileName,
            file.ContentType,
            file.Length,
            stream,
            effectiveOwnerId,
            isAdmin,
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>GET /api/v1/parking-lots/{lotId}/kyb/documents/{fileId} – Tải / xem tài liệu pháp lý hồ sơ KYB (Bảo mật: chỉ Chủ bãi hoặc Admin) (US-054).</summary>
    [HttpGet("documents/{fileId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocument(
        int lotId,
        string fileId,
        [FromQuery] int? ownerProfileId,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerId = currentUser.OwnerProfileId ?? ownerProfileId;
        var isAdmin = currentUser.Roles.Contains("Admin");

        var file = await getDocumentFile.ExecuteAsync(
            lotId,
            fileId,
            effectiveOwnerId,
            isAdmin,
            cancellationToken);

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "private, no-store";
        return File(file.Bytes, file.ContentType, file.FileName);
    }
}
