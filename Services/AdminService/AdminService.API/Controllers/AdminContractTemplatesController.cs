using AdminService.Application.Features.ContractTemplates;
using AdminService.Application.Features.PartnerOnboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;

namespace AdminService.API.Controllers;

/// <summary>
/// US-116: mẫu hợp đồng bãi = các loại hợp đồng có tên (T0–T6) – tạo mới / nhập từ DOCX (AI tùy chọn), soạn bằng form, xem trước,
/// áp dụng (nhiều mẫu Active cùng lúc), đổi tên, thôi áp dụng. Hợp đồng đã sinh giữ file cũ. Chỉ role Admin; người thao tác lấy từ JWT.
/// </summary>
[ApiController]
[Route("api/v1/admin/contract-templates")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminContractTemplatesController(
    IListContractTemplatesUseCase listTemplates,
    IGetContractTemplateUseCase getTemplate,
    IGetContractPlaceholdersUseCase getPlaceholders,
    IGetContractTemplateSourceFileUseCase getSourceFile,
    IPreviewContractTemplateUseCase preview,
    ICreateContractTemplateUseCase create,
    IImportContractTemplateUseCase import,
    IUpdateContractTemplateDraftUseCase updateDraft,
    IRenameContractTemplateUseCase rename,
    IActivateContractTemplateUseCase activate,
    IRetireContractTemplateUseCase retire,
    IConvertContractTemplateUseCase convert,
    IDiscardContractTemplateDraftUseCase discard,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>File DOCX tối đa 10 MB + phần đầu multipart.</summary>
    private const long ImportRequestLimit = 10L * 1024 * 1024 + 64 * 1024;

    /// <summary>GET /api/v1/admin/contract-templates (T0) – mới nhất trước.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ContractTemplateSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ContractTemplateSummaryDto>>> List(CancellationToken cancellationToken)
        => Ok(await listTemplates.ExecuteAsync(cancellationToken));

    /// <summary>GET /api/v1/admin/contract-templates/placeholders (T1p) – trường chèn được vào nội dung.</summary>
    [HttpGet("placeholders")]
    [ProducesResponseType<IReadOnlyList<ContractPlaceholderDto>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ContractPlaceholderDto>> Placeholders() => Ok(getPlaceholders.Execute());

    /// <summary>GET /api/v1/admin/contract-templates/4 (T1).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ContractTemplateDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractTemplateDetailDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await getTemplate.ExecuteAsync(id, cancellationToken));

    /// <summary>GET /api/v1/admin/contract-templates/7/source-file (T1f) – file DOCX gốc.</summary>
    [HttpGet("{id:int}/source-file")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SourceFile(int id, CancellationToken cancellationToken)
        => ContractFileResults.Build(this, await getSourceFile.ExecuteAsync(id, cancellationToken), asAttachment: true);

    /// <summary>POST /api/v1/admin/contract-templates { name, mode: Blank | Duplicate, sourceTemplateId? } (T2) – mẫu mới (Draft).</summary>
    [HttpPost]
    [ProducesResponseType<ContractTemplateDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContractTemplateDetailDto>> Create([FromBody] CreateContractTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminId) return Unauthorized();
        var created = await create.ExecuteAsync(request, adminId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// POST /api/v1/admin/contract-templates/import (T2i) – multipart/form-data: file (.docx ≤ 10 MB), name, useAi.
    /// useAi = true gửi chữ của file cho AI (Anthropic) phân tích; không cấu hình / lỗi → bộ tách thường.
    /// </summary>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImportRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImportRequestLimit)]
    [ProducesResponseType<ContractTemplateDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContractTemplateDetailDto>> Import([FromForm] ImportContractTemplateForm form, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminId) return Unauthorized();
        UploadedFile? upload = null;
        if (form.File is { } file)
        {
            // File quá giới hạn không đọc vào bộ nhớ; use case báo lỗi kích thước.
            var content = Array.Empty<byte>();
            if (file.Length is > 0 and <= 10L * 1024 * 1024)
            {
                using var buffer = new MemoryStream((int)file.Length);
                await file.CopyToAsync(buffer, cancellationToken);
                content = buffer.ToArray();
            }
            upload = new UploadedFile(file.FileName, file.Length, content);
        }
        var created = await import.ExecuteAsync(new ImportContractTemplateRequest(form.Name, upload, form.UseAi), adminId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>PUT /api/v1/admin/contract-templates/5 { name, changeNote, kind, content, body, platform, rowVersion } (T3) – chỉ bản nháp.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<ContractTemplateDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ContractTemplateDetailDto>> UpdateDraft(int id, [FromBody] UpdateContractTemplateDraftRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => updateDraft.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>PATCH /api/v1/admin/contract-templates/5/name { name, rowVersion } (T3n) – đổi tên ở mọi trạng thái.</summary>
    [HttpPatch("{id:int}/name")]
    [ProducesResponseType<ContractTemplateDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ContractTemplateDetailDto>> Rename(int id, [FromBody] RenameContractTemplateRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => rename.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>POST /api/v1/admin/contract-templates/preview { kind, content | body, platform } (T4) – HTML có watermark, không lưu.</summary>
    [HttpPost("preview")]
    [Produces("text/html", "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Preview([FromBody] PreviewContractTemplateRequest request)
        => ContractFileResults.Inline(this, preview.Execute(request));

    /// <summary>POST /api/v1/admin/contract-templates/5/activate { rowVersion, retireSource } (T5).</summary>
    [HttpPost("{id:int}/activate")]
    [ProducesResponseType<ContractTemplateDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ContractTemplateDetailDto>> Activate(int id, [FromBody] ActivateContractTemplateRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => activate.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>POST /api/v1/admin/contract-templates/5/retire { rowVersion } (T5r) – thôi áp dụng.</summary>
    [HttpPost("{id:int}/retire")]
    [ProducesResponseType<ContractTemplateDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<ContractTemplateDetailDto>> Retire(int id, [FromBody] RetireContractTemplateRequest request,
        CancellationToken cancellationToken)
        => AsAdmin(adminId => retire.ExecuteAsync(id, request, adminId, cancellationToken));

    /// <summary>POST /api/v1/admin/contract-templates/3/convert (T6c) – mẫu markup cũ → bản nháp dạng form.</summary>
    [HttpPost("{id:int}/convert")]
    [ProducesResponseType<ContractTemplateDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContractTemplateDetailDto>> Convert(int id, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminId) return Unauthorized();
        var created = await convert.ExecuteAsync(id, adminId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>DELETE /api/v1/admin/contract-templates/5 (T6) – hủy bản nháp.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Discard(int id, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminId) return Unauthorized();
        await discard.ExecuteAsync(id, adminId, cancellationToken);
        return NoContent();
    }

    private async Task<ActionResult<ContractTemplateDetailDto>> AsAdmin(Func<int, Task<ContractTemplateDetailDto>> action)
        => currentUser.UserId is { } adminUserId ? Ok(await action(adminUserId)) : Unauthorized();
}

/// <summary>Form multipart của T2i import.</summary>
public sealed class ImportContractTemplateForm
{
    public IFormFile? File { get; init; }
    public string? Name { get; init; }
    public bool UseAi { get; init; }
}

/// <summary>
/// File hợp đồng (dùng chung cho hợp đồng bãi và mẫu): chặn script / tài nguyên ngoài, không cho trình duyệt đoán kiểu.
/// File không phải HTML (DOCX) luôn trả dạng tải về.
/// </summary>
internal static class ContractFileResults
{
    public static FileContentResult Inline(ControllerBase controller, ContractFileResult file) => Build(controller, file, asAttachment: false);

    public static FileContentResult Build(ControllerBase controller, ContractFileResult file, bool asAttachment)
    {
        controller.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'";
        controller.Response.Headers.XContentTypeOptions = "nosniff";
        var attach = asAttachment || !file.ContentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase);
        return attach
            ? controller.File(file.Content, file.ContentType, file.FileName)
            : controller.File(file.Content, file.ContentType);
    }
}
