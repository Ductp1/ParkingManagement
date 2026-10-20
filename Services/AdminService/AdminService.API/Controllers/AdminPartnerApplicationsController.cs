using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;

namespace AdminService.API.Controllers;

/// <summary>
/// US-116: Admin thẩm định hồ sơ đăng ký đối tác chủ bãi (Partner Onboarding, API 6.1).
/// Controller chỉ điều hướng HTTP → Use case. Chỉ role Admin; người thao tác lấy từ JWT, không nhận từ body.
/// </summary>
[ApiController]
[Route("api/v1/admin/partner-applications")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminPartnerApplicationsController(
    IListPartnerApplicationsUseCase listApplications,
    IGetPartnerApplicationUseCase getApplication,
    IStartPartnerReviewUseCase startReview,
    IRequestPartnerInfoUseCase requestInfo,
    IRejectPartnerApplicationUseCase reject,
    ICancelPartnerApplicationUseCase cancel,
    IPreviewPartnerContractUseCase previewContract,
    ISendPartnerContractUseCase sendContract,
    IRevisePartnerContractUseCase reviseContract,
    IGetPartnerContractFileUseCase getContractFile,
    IIssueOwnerAccountUseCase issueAccount,
    IResendOwnerActivationUseCase resendActivation,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Bản scan tối đa 10 MB + phần đầu multipart.</summary>
    private const long IssueAccountRequestLimit = SignedScanRules.MaxBytes + 64 * 1024;

    /// <summary>GET /api/v1/admin/partner-applications?status=Submitted&amp;search=thu%20duc&amp;page=1&amp;pageSize=20 (A1).</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<PartnerApplicationSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<PartnerApplicationSummaryDto>>> List(
        [FromQuery] PartnerApplicationStatus? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await listApplications.ExecuteAsync(status, search, page, pageSize, cancellationToken));

    /// <summary>GET /api/v1/admin/partner-applications/12 (A2).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<PartnerApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartnerApplicationDetailDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await getApplication.ExecuteAsync(id, cancellationToken));

    /// <summary>POST /api/v1/admin/partner-applications/12/start-review (A3, T2).</summary>
    [HttpPost("{id:int}/start-review")]
    [ProducesResponseType<PartnerApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<PartnerApplicationDetailDto>> StartReview(int id, CancellationToken cancellationToken)
        => AsAdmin(adminId => startReview.ExecuteAsync(id, adminId, cancellationToken));

    /// <summary>POST /api/v1/admin/partner-applications/12/request-info { note } (A4, T3).</summary>
    [HttpPost("{id:int}/request-info")]
    [ProducesResponseType<PartnerApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<PartnerApplicationDetailDto>> RequestInfo(int id, [FromBody] PartnerRequestInfoRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => requestInfo.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>POST /api/v1/admin/partner-applications/12/reject { reason } (A11, T9).</summary>
    [HttpPost("{id:int}/reject")]
    [ProducesResponseType<PartnerApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<PartnerApplicationDetailDto>> Reject(int id, [FromBody] PartnerReasonRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => reject.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>POST /api/v1/admin/partner-applications/12/cancel { reason } (A12, T10).</summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType<PartnerApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<PartnerApplicationDetailDto>> Cancel(int id, [FromBody] PartnerReasonRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => cancel.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>
    /// POST /api/v1/admin/partner-applications/12/contract-preview { commissionRate, effectiveFrom, termMonths, bank } (A5).
    /// Trả file hợp đồng sinh thử (text/html), không lưu, không đổi trạng thái.
    /// </summary>
    [HttpPost("{id:int}/contract-preview")]
    [Produces("text/html", "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PreviewContract(int id, [FromBody] PartnerContractTermsRequest request,
        CancellationToken cancellationToken)
        => ContractFile(await previewContract.ExecuteAsync(id, request, cancellationToken), asAttachment: false);

    /// <summary>POST /api/v1/admin/partner-applications/12/send-contract { commissionRate, effectiveFrom, termMonths, bank } (A6, T5).</summary>
    [HttpPost("{id:int}/send-contract")]
    [ProducesResponseType<PartnerApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<PartnerApplicationDetailDto>> SendContract(int id, [FromBody] PartnerContractTermsRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => sendContract.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>POST /api/v1/admin/partner-applications/12/revise-contract { …terms, reason } (A7, T5b).</summary>
    [HttpPost("{id:int}/revise-contract")]
    [ProducesResponseType<PartnerApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<PartnerApplicationDetailDto>> ReviseContract(int id, [FromBody] PartnerReviseContractRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => reviseContract.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>GET /api/v1/admin/partner-applications/12/contracts/2/file (A8) – tải file hợp đồng, SHA-256 được kiểm tra lại.</summary>
    [HttpGet("{id:int}/contracts/{version:int}/file")]
    [Produces("text/html", "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetContractFile(int id, int version, CancellationToken cancellationToken)
        => ContractFile(await getContractFile.ExecuteAsync(id, version, cancellationToken), asAttachment: true);

    /// <summary>
    /// POST /api/v1/admin/partner-applications/12/issue-account (A9, T6) – multipart/form-data:
    /// signedFile (PDF / JPG / PNG ≤ 10 MB) + confirmedDraftVersion.
    /// </summary>
    [HttpPost("{id:int}/issue-account")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(IssueAccountRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = IssueAccountRequestLimit)]
    [ProducesResponseType<PartnerApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PartnerApplicationDetailDto>> IssueAccount(int id, [FromForm] IssueOwnerAccountForm form,
        CancellationToken cancellationToken)
    {
        UploadedFile? signedFile = null;
        if (form.SignedFile is { } file)
        {
            // File quá giới hạn không đọc vào bộ nhớ; use case báo lỗi kích thước.
            var content = Array.Empty<byte>();
            if (file.Length is > 0 and <= SignedScanRules.MaxBytes)
            {
                using var buffer = new MemoryStream((int)file.Length);
                await file.CopyToAsync(buffer, cancellationToken);
                content = buffer.ToArray();
            }
            signedFile = new UploadedFile(file.FileName, file.Length, content);
        }

        return await AsAdmin(adminId => issueAccount.ExecuteAsync(id,
            new IssueOwnerAccountRequest(form.ConfirmedDraftVersion, signedFile), adminId, cancellationToken));
    }

    /// <summary>POST /api/v1/admin/partner-applications/12/resend-activation (A10, T7).</summary>
    [HttpPost("{id:int}/resend-activation")]
    [ProducesResponseType<PartnerApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<ActionResult<PartnerApplicationDetailDto>> ResendActivation(int id, CancellationToken cancellationToken)
        => AsAdmin(adminId => resendActivation.ExecuteAsync(id, adminId, cancellationToken));

    /// <summary>File hợp đồng HTML: chặn script / tài nguyên ngoài và không cho trình duyệt đoán lại kiểu nội dung.</summary>
    private FileContentResult ContractFile(ContractFileResult file, bool asAttachment)
    {
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'";
        Response.Headers.XContentTypeOptions = "nosniff";
        return asAttachment ? File(file.Content, file.ContentType, file.FileName) : File(file.Content, file.ContentType);
    }

    private async Task<ActionResult<PartnerApplicationDetailDto>> AsAdmin(Func<int, Task<PartnerApplicationDetailDto>> action)
        => currentUser.UserId is { } adminUserId ? Ok(await action(adminUserId)) : Unauthorized();
}

/// <summary>Form multipart của A9 issue-account.</summary>
public sealed class IssueOwnerAccountForm
{
    public IFormFile? SignedFile { get; init; }
    public int? ConfirmedDraftVersion { get; init; }
}
