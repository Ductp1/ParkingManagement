using GateService.Application.Features.PmsIntegration;
using Microsoft.AspNetCore.Mvc;

namespace GateService.API.Controllers;

/// <summary>
/// US-052 / UC-51: tích hợp PMS Mức 1 – PMS đẩy sự kiện xe vào/ra; Staff xử lý hàng chờ sự kiện không khớp.
/// Controller chỉ điều hướng HTTP → Use case, không chứa nghiệp vụ.
/// </summary>
[ApiController]
[Route("api/v1/gate/pms")]
public sealed class PmsController(
    IProcessPmsEventUseCase processEvent,
    IListQuarantinedPmsEventsUseCase listQuarantined,
    IResolveQuarantinedPmsEventUseCase resolveQuarantined) : ControllerBase
{
    public const string ApiKeyHeader = "X-Api-Key";

    /// <summary>
    /// POST /api/v1/gate/pms/events – Header X-Api-Key: "{prefix}.{secret}".
    /// Body: { eventId (Guid, idempotency), eventType: "VehicleEntry"|"VehicleExit", parkingLotId, plateNumber,
    /// occurredAtUtc, bookingCode?, gateId?, imageUrl? }.
    /// 200 + outcome: SessionActive / SessionClosed / Duplicate (đã xử lý) / Quarantined (chờ Staff xử lý tay).
    /// 401 = key sai; 403 = key không thuộc bãi; 503 = lỗi tạm thời → PMS retry cùng eventId.
    /// </summary>
    [HttpPost("events")]
    [ProducesResponseType<PmsEventResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PmsEventResultDto>> ReceiveEvent(
        [FromHeader(Name = ApiKeyHeader)] string? apiKey, [FromBody] PmsEventCommand command, CancellationToken cancellationToken)
        => Ok(await processEvent.ExecuteAsync(apiKey ?? string.Empty, command, cancellationToken));

    /// <summary>GET /api/v1/gate/pms/quarantine?parkingLotId=2 – sự kiện PMS không khớp đang chờ Staff xử lý (UC-51 E1).</summary>
    [HttpGet("quarantine")]
    [ProducesResponseType<IReadOnlyList<PmsUnmatchedEventDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PmsUnmatchedEventDto>>> ListQuarantine(
        [FromQuery] int? parkingLotId, CancellationToken cancellationToken)
        => Ok(await listQuarantined.ExecuteAsync(parkingLotId, cancellationToken));

    /// <summary>
    /// POST /api/v1/gate/pms/quarantine/{id}/resolve – Body: { staffUserId, note?, discard? }.
    /// Staff đánh dấu đã xử lý tay (Handled) hoặc bỏ qua (Discarded); xử lý rồi → 409.
    /// </summary>
    [HttpPost("quarantine/{id:int}/resolve")]
    [ProducesResponseType<PmsUnmatchedEventDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PmsUnmatchedEventDto>> ResolveQuarantine(
        int id, [FromBody] ResolveQuarantinedPmsEventCommand command, CancellationToken cancellationToken)
        => Ok(await resolveQuarantined.ExecuteAsync(id, command, cancellationToken));
}
