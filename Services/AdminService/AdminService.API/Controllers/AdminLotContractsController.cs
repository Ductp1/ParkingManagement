using AdminService.Application.Features.LotContracts;
using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;

namespace AdminService.API.Controllers;

/// <summary>
/// US-116: hợp đồng hợp tác khai thác từng bãi đỗ (mục 14.4 – L0–L4). Lập sau khi KYB của bãi được duyệt; ký xong thì bãi được kích hoạt.
/// Controller chỉ điều hướng HTTP → Use case. Chỉ role Admin; người thao tác lấy từ JWT, không nhận từ body.
/// </summary>
[ApiController]
[Route("api/v1/admin/lot-contracts")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminLotContractsController(
    IListLotContractsUseCase listContracts,
    IGetLotContractUseCase getContract,
    IGetLotContractFileUseCase getFile,
    IPreviewNewLotContractUseCase previewNew,
    IPreviewLotContractRevisionUseCase previewRevision,
    ICreateLotContractUseCase create,
    IReviseLotContractUseCase revise,
    ISignLotContractUseCase sign,
    ICancelLotContractUseCase cancel,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Bản scan tối đa 10 MB + phần đầu multipart.</summary>
    private const long SignRequestLimit = SignedScanRules.MaxBytes + 64 * 1024;

    /// <summary>GET /api/v1/admin/lot-contracts?status=Pending&amp;parkingLotId=3&amp;ownerProfileId=1&amp;search=&amp;page=1&amp;pageSize=20 (L0).</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<LotContractSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<LotContractSummaryDto>>> List([FromQuery] LotContractStatus? status,
        [FromQuery] int? parkingLotId, [FromQuery] int? ownerProfileId, [FromQuery] string? search, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        => Ok(await listContracts.ExecuteAsync(status, parkingLotId, ownerProfileId, search, page, pageSize, cancellationToken));

    /// <summary>GET /api/v1/admin/lot-contracts/5 (L0b).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<LotContractDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotContractDetailDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await getContract.ExecuteAsync(id, cancellationToken));

    /// <summary>POST /api/v1/admin/lot-contracts/preview { kybApplicationId, commissionRate, effectiveFrom, termMonths } (L1p) – HTML, không lưu.</summary>
    [HttpPost("preview")]
    [Produces("text/html", "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> PreviewNew([FromBody] CreateLotContractRequest request, CancellationToken cancellationToken)
        => currentUser.UserId is { } adminId
            ? ContractFile(await previewNew.ExecuteAsync(request, adminId, cancellationToken), asAttachment: false)
            : Unauthorized();

    /// <summary>POST /api/v1/admin/lot-contracts { kybApplicationId, commissionRate, effectiveFrom, termMonths } (L1) – lập hợp đồng v1.</summary>
    [HttpPost]
    [ProducesResponseType<LotContractDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<ActionResult<LotContractDetailDto>> Create([FromBody] CreateLotContractRequest request, CancellationToken cancellationToken)
        => AsAdmin(adminId => create.ExecuteAsync(request, adminId, cancellationToken));

    /// <summary>POST /api/v1/admin/lot-contracts/5/preview { commissionRate, effectiveFrom, termMonths } (L2p) – xem trước v+1.</summary>
    [HttpPost("{id:int}/preview")]
    [Produces("text/html", "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PreviewRevision(int id, [FromBody] LotContractTermsRequest request, CancellationToken cancellationToken)
        => ContractFile(await previewRevision.ExecuteAsync(id, request, cancellationToken), asAttachment: false);

    /// <summary>POST /api/v1/admin/lot-contracts/5/revise { commissionRate, effectiveFrom, termMonths, reason } (L2).</summary>
    [HttpPost("{id:int}/revise")]
    [ProducesResponseType<LotContractDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<LotContractDetailDto>> Revise(int id, [FromBody] ReviseLotContractRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => revise.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>GET /api/v1/admin/lot-contracts/5/versions/2/file (L2f) – tải file, SHA-256 được kiểm tra lại.</summary>
    [HttpGet("{id:int}/versions/{version:int}/file")]
    [Produces("text/html", "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetVersionFile(int id, int version, CancellationToken cancellationToken)
        => ContractFile(await getFile.ExecuteAsync(id, version, cancellationToken), asAttachment: true);

    /// <summary>
    /// POST /api/v1/admin/lot-contracts/5/sign (L3) – multipart/form-data: signedFile (PDF / JPG / PNG ≤ 10 MB) + confirmedVersion + note.
    /// Ký hợp đồng = duyệt KYB (design v9): ParkingService duyệt KYB và kích hoạt bãi rồi hợp đồng chuyển Signed.
    /// Kích hoạt bãi bên ParkingService rồi chuyển hợp đồng sang Signed.
    /// </summary>
    [HttpPost("{id:int}/sign")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(SignRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = SignRequestLimit)]
    [ProducesResponseType<LotContractDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<LotContractDetailDto>> Sign(int id, [FromForm] SignLotContractForm form, CancellationToken cancellationToken)
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

        return await AsAdmin(adminId => sign.ExecuteAsync(id, new SignLotContractRequest(form.ConfirmedVersion, signedFile, form.Note), adminId,
            cancellationToken));
    }

    /// <summary>POST /api/v1/admin/lot-contracts/5/cancel { reason } (L4).</summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType<LotContractDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<LotContractDetailDto>> Cancel(int id, [FromBody] LotContractReasonRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => cancel.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>File hợp đồng HTML: chặn script / tài nguyên ngoài và không cho trình duyệt đoán lại kiểu nội dung.</summary>
    private FileContentResult ContractFile(ContractFileResult file, bool asAttachment)
    {
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'";
        Response.Headers.XContentTypeOptions = "nosniff";
        return asAttachment ? File(file.Content, file.ContentType, file.FileName) : File(file.Content, file.ContentType);
    }

    private async Task<ActionResult<LotContractDetailDto>> AsAdmin(Func<int, Task<LotContractDetailDto>> action)
        => currentUser.UserId is { } adminUserId ? Ok(await action(adminUserId)) : Unauthorized();
}

/// <summary>Form multipart của L3 sign.</summary>
public sealed class SignLotContractForm
{
    public IFormFile? SignedFile { get; init; }
    public int? ConfirmedVersion { get; init; }
    /// <summary>Ghi chú duyệt KYB (tùy chọn).</summary>
    public string? Note { get; init; }
}
