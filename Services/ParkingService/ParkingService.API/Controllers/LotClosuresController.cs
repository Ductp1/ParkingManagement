using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Application.Features.Closures;

namespace ParkingService.API.Controllers;

/// <summary>
/// Controller quản lý lịch tạm đóng một phần hoặc toàn bộ bãi đỗ xe: Zone / Floor / Slot / Lot (US-063).
/// Phục vụ nghiệp vụ bảo trì, sự kiện, vệ sinh và Emergency Stop.
/// </summary>
[ApiController]
[Route("api/v1/parking-lots/{lotId:int}/closures")]
public sealed class LotClosuresController(
    ICreateClosureScheduleUseCase createClosureSchedule,
    IValidateClosureImpactUseCase validateClosureImpact,
    ICancelClosureScheduleUseCase cancelClosureSchedule,
    IGetLotClosureSchedulesUseCase getLotClosureSchedules,
    IGetClosureScheduleByIdUseCase getClosureScheduleById,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/parking-lots/{lotId}/closures
    /// Tạo lịch tạm đóng một phần bãi (Zone/Floor/Slot) hoặc toàn bộ bãi đỗ (US-063).
    /// Chặn và báo lỗi 409 Conflict nếu có ô đỗ đang có xe (Occupied) hoặc giữ chỗ (Reserved) khi không bật chế độ khẩn cấp.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<ClosureScheduleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClosureScheduleDto>> Create(
        int lotId,
        [FromBody] CreateClosureScheduleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateClosureScheduleCommand(
            lotId,
            request.TargetType,
            request.TargetId,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.Reason,
            request.IsEmergency,
            currentUser.UserId ?? 0);

        var result = await createClosureSchedule.ExecuteAsync(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// POST /api/v1/parking-lots/{lotId}/closures/impact-check
    /// Pre-flight impact check kiểm tra trước tác động an toàn và danh sách ô đỗ có xe trước khi đóng (US-063).
    /// </summary>
    [HttpPost("impact-check")]
    [ProducesResponseType<ClosureImpactCheckResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClosureImpactCheckResultDto>> ImpactCheck(
        int lotId,
        [FromBody] ClosureImpactCheckRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ClosureImpactCheckCommand(
            lotId,
            request.TargetType,
            request.TargetId,
            request.StartsAtUtc,
            request.EndsAtUtc,
            request.IsEmergency);

        var result = await validateClosureImpact.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/v1/parking-lots/{lotId}/closures
    /// Lấy danh sách lịch tạm đóng của bãi xe, có thể lọc theo TargetType và lọc chỉ lấy lịch đang có hiệu lực (US-063).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ClosureScheduleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ClosureScheduleDto>>> GetAll(
        int lotId,
        [FromQuery] ClosureTargetType? targetType,
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken)
    {
        var result = await getLotClosureSchedules.ExecuteAsync(lotId, targetType, activeOnly, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/v1/parking-lots/{lotId}/closures/{closureId:int}
    /// Lấy chi tiết một lịch tạm đóng của bãi xe (US-063).
    /// </summary>
    [HttpGet("{closureId:int}")]
    [ProducesResponseType<ClosureScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClosureScheduleDto>> GetById(
        int lotId,
        int closureId,
        CancellationToken cancellationToken)
    {
        var result = await getClosureScheduleById.ExecuteAsync(lotId, closureId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/v1/parking-lots/{lotId}/closures/{closureId:int}/cancel
    /// Hủy lịch tạm đóng / Mở lại trước hạn và hoàn nguyên trạng thái ô đỗ về Available (US-063).
    /// </summary>
    [HttpPost("{closureId:int}/cancel")]
    [ProducesResponseType<ClosureScheduleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClosureScheduleDto>> Cancel(
        int lotId,
        int closureId,
        [FromBody] string? reason,
        CancellationToken cancellationToken)
    {
        var command = new CancelClosureScheduleCommand(
            lotId,
            closureId,
            reason,
            currentUser.UserId ?? 0);

        var result = await cancelClosureSchedule.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }
}
