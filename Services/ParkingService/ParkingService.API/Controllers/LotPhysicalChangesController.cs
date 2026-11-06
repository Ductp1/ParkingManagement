using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Application.Features.PhysicalChanges;

namespace ParkingService.API.Controllers;

/// <summary>
/// Controller quản lý thay đổi vật lý và giám sát hạn cam kết SLA 48h (US-061, EP09).
/// Tiêu chí 1: Khai báo thay đổi vật lý -> đếm ngược 48h và gửi cảnh báo nhắc nhở.
/// Tiêu chí 2: Quá 48h chưa cập nhật mà có khách đặt nhầm -> chủ bãi chịu 100% bồi thường và ghi nhận vi phạm SLA Cấp 2.
/// Tuyến đường: /api/v1/parking-lots/{lotId:int}/physical-changes
/// </summary>
[ApiController]
[Route("api/v1/parking-lots/{lotId:int}/physical-changes")]
public sealed class LotPhysicalChangesController(
    IDeclarePhysicalChangeUseCase declarePhysicalChange,
    IGetLotPhysicalChangesUseCase getLotPhysicalChanges,
    IGetPhysicalChangeByIdUseCase getPhysicalChangeById,
    ISendPhysicalChangeReminderUseCase sendPhysicalChangeReminder,
    ICompletePhysicalChangeUseCase completePhysicalChange,
    IRecordPhysicalChangeBookingErrorUseCase recordPhysicalChangeBookingError,
    ICancelPhysicalChangeUseCase cancelPhysicalChange,
    IGetPhysicalChangeSlaSummaryUseCase getPhysicalChangeSlaSummary,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>POST /api/v1/parking-lots/{lotId}/physical-changes - Khai báo thay đổi vật lý (Tiêu chí 1: kích hoạt đếm ngược 48h).</summary>
    [HttpPost]
    [ProducesResponseType<LotPhysicalChangeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotPhysicalChangeDto>> Declare(
        int lotId,
        [FromBody] DeclarePhysicalChangeRequest request,
        [FromQuery] int? ownerProfileId,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerId = ownerProfileId ?? currentUser.OwnerProfileId;
        var result = await declarePhysicalChange.ExecuteAsync(lotId, request, effectiveOwnerId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>GET /api/v1/parking-lots/{lotId}/physical-changes - Danh sách các đợt thay đổi vật lý và đếm ngược 48h.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<LotPhysicalChangeDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LotPhysicalChangeDto>>> GetAll(
        int lotId,
        CancellationToken cancellationToken)
        => Ok(await getLotPhysicalChanges.ExecuteAsync(lotId, cancellationToken));

    /// <summary>GET /api/v1/parking-lots/{lotId}/physical-changes/{changeId} - Xem chi tiết một đợt thay đổi vật lý.</summary>
    [HttpGet("{changeId:int}")]
    [ProducesResponseType<LotPhysicalChangeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotPhysicalChangeDto>> GetById(
        int lotId,
        int changeId,
        CancellationToken cancellationToken)
        => Ok(await getPhysicalChangeById.ExecuteAsync(lotId, changeId, cancellationToken));

    /// <summary>POST /api/v1/parking-lots/{lotId}/physical-changes/{changeId}/send-reminder - Gửi nhắc nhở cập nhật sơ đồ (Tiêu chí 1).</summary>
    [HttpPost("{changeId:int}/send-reminder")]
    [ProducesResponseType<PhysicalChangeReminderResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PhysicalChangeReminderResultDto>> SendReminder(
        int lotId,
        int changeId,
        [FromQuery] int? ownerProfileId,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerId = ownerProfileId ?? currentUser.OwnerProfileId;
        var result = await sendPhysicalChangeReminder.ExecuteAsync(lotId, changeId, effectiveOwnerId, cancellationToken);
        return Ok(result);
    }

    /// <summary>POST /api/v1/parking-lots/{lotId}/physical-changes/{changeId}/complete - Hoàn tất cập nhật sơ đồ sau thay đổi vật lý.</summary>
    [HttpPost("{changeId:int}/complete")]
    [ProducesResponseType<LotPhysicalChangeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LotPhysicalChangeDto>> Complete(
        int lotId,
        int changeId,
        [FromBody] CompletePhysicalChangeRequest request,
        [FromQuery] int? ownerProfileId,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerId = ownerProfileId ?? currentUser.OwnerProfileId;
        var result = await completePhysicalChange.ExecuteAsync(lotId, changeId, request, effectiveOwnerId, cancellationToken);
        return Ok(result);
    }

    /// <summary>POST /api/v1/parking-lots/{lotId}/physical-changes/{changeId}/report-booking-error - Khách đặt nhầm quá 48h: 100% bồi thường & vi phạm SLA Cấp 2 (Tiêu chí 2).</summary>
    [HttpPost("{changeId:int}/report-booking-error")]
    [ProducesResponseType<LotPhysicalChangeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LotPhysicalChangeDto>> ReportBookingError(
        int lotId,
        int changeId,
        [FromBody] RecordPhysicalChangeBookingErrorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await recordPhysicalChangeBookingError.ExecuteAsync(lotId, changeId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>POST /api/v1/parking-lots/{lotId}/physical-changes/{changeId}/cancel - Hủy đợt thay đổi vật lý.</summary>
    [HttpPost("{changeId:int}/cancel")]
    [ProducesResponseType<LotPhysicalChangeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LotPhysicalChangeDto>> Cancel(
        int lotId,
        int changeId,
        [FromBody] CancelPhysicalChangeRequest request,
        [FromQuery] int? ownerProfileId,
        CancellationToken cancellationToken)
    {
        var effectiveOwnerId = ownerProfileId ?? currentUser.OwnerProfileId;
        var result = await cancelPhysicalChange.ExecuteAsync(lotId, changeId, request, effectiveOwnerId, cancellationToken);
        return Ok(result);
    }

    /// <summary>GET /api/v1/parking-lots/{lotId}/physical-changes/sla-summary - Xem báo cáo tổng hợp tỷ lệ tuân thủ SLA và vi phạm Cấp 2.</summary>
    [HttpGet("sla-summary")]
    [ProducesResponseType<PhysicalChangeSlaSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PhysicalChangeSlaSummaryDto>> GetSlaSummary(
        int lotId,
        CancellationToken cancellationToken)
        => Ok(await getPhysicalChangeSlaSummary.ExecuteAsync(lotId, cancellationToken));
}
