using AdminService.API.Security;
using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Enums;
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

    /// <summary>Giới hạn body như nộp hồ sơ: giấy tờ ≤ 25 MB + trường chữ.</summary>
    private const long RequestLimit = PartnerDocumentRules.MaxTotalBytes + 2 * 1024 * 1024;

    /// <summary>
    /// PUT /api/v1/partner-applications/PA-2026-0012/resubmit (header X-Edit-Token) – P3 / T4: gửi bổ sung → UnderReview.
    /// multipart/form-data: các trường như form nộp hồ sơ (mục không được yêu cầu phải giữ nguyên) + file của các loại giấy tờ được yêu cầu
    /// (businessLicenseFiles, lotLegalProofFiles, idCardFrontFiles, idCardBackFiles, otherFiles).
    /// </summary>
    [HttpPut("{code}/resubmit")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimit)]
    [EnableRateLimiting(PartnerApplicationRateLimit.EditPolicy)]
    [ProducesResponseType<PartnerApplicationReceiptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PartnerApplicationReceiptDto>> Resubmit(string code, [FromForm] PartnerRegistrationForm form,
        CancellationToken cancellationToken)
    {
        var documents = new List<PartnerDocumentUpload>();
        await AddAsync(documents, PartnerDocumentType.BusinessLicense, form.BusinessLicenseFiles, cancellationToken);
        await AddAsync(documents, PartnerDocumentType.LotLegalProof, form.LotLegalProofFiles, cancellationToken);
        await AddAsync(documents, PartnerDocumentType.IdCardFront, form.IdCardFrontFiles, cancellationToken);
        await AddAsync(documents, PartnerDocumentType.IdCardBack, form.IdCardBackFiles, cancellationToken);
        await AddAsync(documents, PartnerDocumentType.Other, form.OtherFiles, cancellationToken);
        var request = new SubmitPartnerApplicationRequest(form.FullName, form.Email, form.Phone, form.Position, form.BusinessType,
            form.BusinessName, form.TaxCode, form.BusinessAddress, form.LegalRepresentativeName, form.LegalRepresentativePosition,
            form.LotCount, form.PrimaryLotAddress, form.EstimatedCarSlots, form.EstimatedMotorbikeSlots, form.OperatingHours,
            form.ConsentAccepted);
        return Ok(await resubmit.ExecuteAsync(code, Request.Headers[EditTokenHeader].ToString(), request, documents, cancellationToken));
    }

    private static async Task AddAsync(List<PartnerDocumentUpload> documents, PartnerDocumentType type, IReadOnlyList<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        foreach (var file in files ?? [])
        {
            // File quá giới hạn hoặc quá nhiều file: không đọc vào bộ nhớ, use case báo lỗi.
            var content = Array.Empty<byte>();
            if (file.Length is > 0 and <= PartnerDocumentRules.MaxFileBytes && documents.Count < PartnerDocumentRules.MaxFiles)
            {
                using var buffer = new MemoryStream((int)file.Length);
                await file.CopyToAsync(buffer, cancellationToken);
                content = buffer.ToArray();
            }
            documents.Add(new PartnerDocumentUpload(type, new UploadedFile(file.FileName, file.Length, content)));
        }
    }
}
