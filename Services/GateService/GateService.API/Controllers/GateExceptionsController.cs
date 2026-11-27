using GateService.Application.Features.GateEvents;
using GateService.Application.Features.GateExceptions;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.API.Controllers;

/// <summary>
/// US-048 / UC-23: xử lý ngoại lệ thủ công tại cổng (QR/biển số lỗi, thanh toán lỗi, hỏng barie...).
/// Controller chỉ điều hướng HTTP → Use case, không chứa nghiệp vụ.
/// </summary>
[ApiController]
[Route("api/v1/gate/exceptions")]
public sealed class GateExceptionsController(
    IRecordGateExceptionUseCase recordException,
    IListGateExceptionsUseCase listExceptions) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/gate/exceptions – Staff ghi nhận + xử lý ngoại lệ.
    /// Body: { parkingLotId, action: "RecordOnly"|"ManualCheckIn"|"ManualCheckOut"|"ConfirmCashPayment"|"EmergencyBarrierOpen",
    /// exceptionType: "OcrFailed"|"ManualPlateCorrection"|"PaymentFailed"|"EmergencyBarrierOpen"|"VehicleArrived",
    /// reason (bắt buộc), staffUserId, plate?, bookingCode?, parkingSessionId?, imagePath? }.
    /// Mọi lượt xử lý đều ghi GateEvents (staff, thời điểm, lý do, ảnh) và tăng bộ đếm ngoại lệ của ca trực.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<GateExceptionResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GateExceptionResultDto>> Record([FromBody] GateExceptionCommand command, CancellationToken cancellationToken)
        => Ok(await recordException.ExecuteAsync(command, cancellationToken));

    /// <summary>
    /// GET /api/v1/gate/exceptions?parkingLotId=2&amp;exceptionType=PaymentFailed&amp;fromUtc=...&amp;toUtc=...&amp;limit=100 –
    /// audit log thao tác thủ công tại cổng cho Chủ bãi/Admin đối soát, mới nhất trước.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GateEventDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<GateEventDto>>> List(
        [FromQuery] int parkingLotId, [FromQuery] GateEventType? exceptionType, [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc, [FromQuery] int limit = 100, CancellationToken cancellationToken = default)
        => Ok(await listExceptions.ExecuteAsync(
            new GateExceptionLogQuery(parkingLotId, exceptionType, fromUtc, toUtc, limit), cancellationToken));
}
