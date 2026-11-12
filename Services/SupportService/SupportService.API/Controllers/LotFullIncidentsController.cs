using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;

namespace SupportService.API.Controllers;

/// <summary>
/// US-091 – Khách đến nơi nhưng bãi hết chỗ. Tài xế báo sự cố, chọn bãi thay thế hoặc nhận hoàn tiền;
/// Admin theo dõi sự cố và vi phạm SLA của bãi. UserId luôn lấy từ JWT (claim "sub").
/// </summary>
[ApiController]
[Route("api/v1/lot-full-incidents")]
public sealed class LotFullIncidentsController(ILotFullIncidentService incidents, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>POST /api/v1/lot-full-incidents – AC1: "Báo sự cố" bãi hết chỗ → sự cố ưu tiên cao + danh sách bãi thay thế trong 1 km.</summary>
    [HttpPost]
    [Authorize(Policy = "RequireDriver")]
    [ProducesResponseType<LotFullIncidentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<LotFullIncidentDto>> Report([FromBody] ReportLotFullRequestDto request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();
        var incident = await incidents.ReportAsync(userId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { code = incident.Code }, incident);
    }

    /// <summary>GET /api/v1/lot-full-incidents/LF-20261120-1234 – Tài xế xem sự cố của mình.</summary>
    [HttpGet("{code}")]
    [Authorize(Policy = "RequireDriver")]
    [ProducesResponseType<LotFullIncidentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotFullIncidentDto>> Get([FromRoute] string code, CancellationToken cancellationToken)
        => currentUser.UserId is { } userId ? Ok(await incidents.GetAsync(userId, code, cancellationToken)) : Unauthorized();

    /// <summary>POST /api/v1/lot-full-incidents/{code}/accept-transfer – AC2: đồng ý chuyển miễn phí sang 1 bãi đã gợi ý.</summary>
    [HttpPost("{code}/accept-transfer")]
    [Authorize(Policy = "RequireDriver")]
    [ProducesResponseType<LotFullIncidentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<LotFullIncidentDto>> AcceptTransfer([FromRoute] string code, [FromBody] AcceptTransferRequestDto request,
        CancellationToken cancellationToken)
        => currentUser.UserId is { } userId ? Ok(await incidents.AcceptTransferAsync(userId, code, request, cancellationToken)) : Unauthorized();

    /// <summary>POST /api/v1/lot-full-incidents/{code}/decline – AC3: không nhận bãi thay thế → hoàn 100% + voucher, bãi vi phạm SLA.</summary>
    [HttpPost("{code}/decline")]
    [Authorize(Policy = "RequireDriver")]
    [ProducesResponseType<LotFullIncidentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<LotFullIncidentDto>> Decline([FromRoute] string code, CancellationToken cancellationToken)
        => currentUser.UserId is { } userId ? Ok(await incidents.DeclineAsync(userId, code, cancellationToken)) : Unauthorized();

    /// <summary>GET /api/v1/lot-full-incidents?parkingLotId=1&amp;status=RefundRequested&amp;slaViolationOnly=true – Admin theo dõi sự cố / vi phạm SLA.</summary>
    [HttpGet]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType<IReadOnlyList<LotFullIncidentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<LotFullIncidentDto>>> List([FromQuery] int? parkingLotId, [FromQuery] string? status,
        [FromQuery] bool slaViolationOnly = false, CancellationToken cancellationToken = default)
        => Ok(await incidents.ListForAdminAsync(parkingLotId, status, slaViolationOnly, cancellationToken));
}
