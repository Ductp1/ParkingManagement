using AdminService.API.Security;
using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ParkingManagement.SharedKernel.Contracts;

namespace AdminService.API.Controllers;

/// <summary>
/// US-053 (T1): "Trở thành đối tác chủ bãi". Người dùng phải ĐĂNG NHẬP trước (mọi vai trò trừ người đã là chủ bãi); hồ sơ gắn với
/// tài khoản trong JWT, chờ Admin thẩm định rồi duyệt. Nằm dưới /api/v1/admin vì Gateway chỉ chuyển tiền tố đó sang AdminService.
/// Người nộp chỉ xem được trạng thái hồ sơ của mình (GET mine) – giấy tờ pháp lý chỉ Admin xem qua AdminPartnerApplicationsController.
/// Giới hạn tần suất theo IP (policy "partner-registration").
/// </summary>
[ApiController]
[Route("api/v1/admin/partner-registrations")]
[Authorize]
public sealed class PartnerRegistrationsController(
    ISubmitPartnerApplicationUseCase submit,
    IListMyPartnerApplicationsUseCase listMine,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Gateway (Kestrel) giới hạn body ~30 MB; để dư cho các trường chữ.</summary>
    private const long RequestLimit = PartnerDocumentRules.MaxTotalBytes + 2 * 1024 * 1024;

    /// <summary>
    /// POST /api/v1/admin/partner-registrations – multipart/form-data: thông tin người đăng ký, pháp nhân, bãi dự kiến
    /// + businessLicenseFiles, lotLegalProofFiles (bắt buộc, PDF / JPG / PNG), idCardFrontFiles, idCardBackFiles (bắt buộc, đúng 1 ảnh
    /// JPG / PNG mỗi mặt), otherFiles (tùy chọn). Mỗi file ≤ 10 MB.
    /// Trả mã hồ sơ; hồ sơ ở trạng thái Submitted chờ Admin thẩm định.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimit)]
    [EnableRateLimiting(PartnerRegistrationRateLimit.PolicyName)]
    [ProducesResponseType<PartnerApplicationReceiptDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PartnerApplicationReceiptDto>> Submit([FromForm] PartnerRegistrationForm form,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
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
        var receipt = await submit.ExecuteAsync(new PartnerApplicant(userId, IsLotOwner()), request, documents, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, receipt);
    }

    /// <summary>
    /// GET /api/v1/admin/partner-registrations/mine – hồ sơ đăng ký của chính tài khoản đang đăng nhập (trạng thái, yêu cầu bổ sung,
    /// lý do từ chối). Không trả giấy tờ.
    /// </summary>
    [HttpGet("mine")]
    [ProducesResponseType<IReadOnlyList<MyPartnerApplicationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<MyPartnerApplicationDto>>> Mine(CancellationToken cancellationToken)
        => currentUser.UserId is { } userId ? Ok(await listMine.ExecuteAsync(userId, cancellationToken)) : Unauthorized();

    /// <summary>Đã là chủ bãi: token có OwnerProfileId hoặc role chủ bãi.</summary>
    private bool IsLotOwner() => currentUser.OwnerProfileId is not null
                                 || currentUser.Roles.Any(r => r is "LotOwner" or "Owner");

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

/// <summary>Form multipart của POST partner-registrations.</summary>
public sealed class PartnerRegistrationForm
{
    public string? FullName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Position { get; init; }
    public PartnerBusinessType? BusinessType { get; init; }
    public string? BusinessName { get; init; }
    public string? TaxCode { get; init; }
    public string? BusinessAddress { get; init; }
    public string? LegalRepresentativeName { get; init; }
    public string? LegalRepresentativePosition { get; init; }
    public int? LotCount { get; init; }
    public string? PrimaryLotAddress { get; init; }
    public int? EstimatedCarSlots { get; init; }
    public int? EstimatedMotorbikeSlots { get; init; }
    public string? OperatingHours { get; init; }
    public bool ConsentAccepted { get; init; }
    public List<IFormFile>? BusinessLicenseFiles { get; init; }
    public List<IFormFile>? LotLegalProofFiles { get; init; }
    public List<IFormFile>? IdCardFrontFiles { get; init; }
    public List<IFormFile>? IdCardBackFiles { get; init; }
    public List<IFormFile>? OtherFiles { get; init; }
}
