using Microsoft.AspNetCore.Mvc;
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
/// </summary>
[ApiController]
[Route("api/v1/parking-lots/{lotId:int}/kyb")]
public sealed class KybApplicationsController(
    IGetKybApplicationUseCase getKybApplication,
    ISaveKybDraftUseCase saveKybDraft,
    ISubmitKybApplicationUseCase submitKybApplication) : ControllerBase
{
    /// <summary>GET /api/v1/parking-lots/{lotId}/kyb – Lấy thông tin / khởi tạo hồ sơ KYB của bãi đỗ.</summary>
    [HttpGet]
    [ProducesResponseType<KybApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KybApplicationDto>> GetKybApplication(
        int lotId,
        [FromQuery] int ownerProfileId,
        CancellationToken cancellationToken)
    {
        var query = new GetKybApplicationQuery(lotId, ownerProfileId);
        return Ok(await getKybApplication.ExecuteAsync(query, cancellationToken));
    }

    /// <summary>PUT /api/v1/parking-lots/{lotId}/kyb/draft – Lưu nháp tiến độ hồ sơ KYB (cho phép lưu từng bước).</summary>
    [HttpPut("draft")]
    [ProducesResponseType<KybApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KybApplicationDto>> SaveDraft(
        int lotId,
        [FromBody] SaveKybDraftRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SaveKybDraftCommand(
            lotId,
            request.OwnerProfileId,
            request.BusinessLicenseUrl,
            request.SitePhotoUrls,
            request.PhotoLatitude,
            request.PhotoLongitude,
            request.FireSafetyCertificateUrl,
            request.FieldSurveyAtUtc,
            request.FieldSurveyNote);

        return Ok(await saveKybDraft.ExecuteAsync(command, cancellationToken));
    }

    /// <summary>POST /api/v1/parking-lots/{lotId}/kyb/submit – Nộp chính thức hồ sơ KYB (kiểm tra đầy đủ 4 bước).</summary>
    [HttpPost("submit")]
    [ProducesResponseType<KybApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KybApplicationDto>> Submit(
        int lotId,
        [FromBody] SubmitKybApplicationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SubmitKybApplicationCommand(
            lotId,
            request.OwnerProfileId,
            request.BusinessLicenseUrl,
            request.SitePhotoUrls,
            request.PhotoLatitude,
            request.PhotoLongitude,
            request.FireSafetyCertificateUrl,
            request.FieldSurveyAtUtc,
            request.FieldSurveyNote);

        return Ok(await submitKybApplication.ExecuteAsync(command, cancellationToken));
    }
}
