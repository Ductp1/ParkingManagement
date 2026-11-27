using GateService.Application.Features.Shifts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.API.Controllers;

/// <summary>US-076: ca trực. Phân quyền theo bãi / theo ca nằm trong use case; controller chỉ điều hướng HTTP.</summary>
[ApiController]
[Authorize]
[Route("api/v1/gate")]
public sealed class ShiftsController(ShiftPlanningUseCases planning, ShiftDutyUseCases duty) : ControllerBase
{
    // ===== Chủ bãi: mẫu ca =====
    [HttpGet("lots/{lotId:int}/shift-templates")]
    public async Task<ActionResult<IReadOnlyList<ShiftTemplateDto>>> Templates(int lotId, CancellationToken ct)
        => Ok(await planning.TemplatesAsync(lotId, ct));

    [HttpPost("lots/{lotId:int}/shift-templates")]
    public async Task<ActionResult<ShiftTemplateDto>> CreateTemplate(int lotId, [FromBody] ShiftTemplateRequest request, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await planning.SaveTemplateAsync(lotId, null, request, ct));

    [HttpPut("lots/{lotId:int}/shift-templates/{templateId:int}")]
    public async Task<ActionResult<ShiftTemplateDto>> UpdateTemplate(int lotId, int templateId, [FromBody] ShiftTemplateRequest request,
        CancellationToken ct) => Ok(await planning.SaveTemplateAsync(lotId, templateId, request, ct));

    [HttpDelete("lots/{lotId:int}/shift-templates/{templateId:int}")]
    public async Task<IActionResult> DeleteTemplate(int lotId, int templateId, CancellationToken ct)
    {
        await planning.DeleteTemplateAsync(lotId, templateId, ct);
        return NoContent();
    }

    // ===== Chủ bãi: lịch, bàn giao =====
    /// <summary>?weekStart=2026-11-30 (ngày giờ Việt Nam) – ca của tuần + cảnh báo giờ mở cửa chưa có người / chưa có người giữ két.</summary>
    [HttpGet("lots/{lotId:int}/shifts")]
    public async Task<ActionResult<ShiftScheduleDto>> Week(int lotId, [FromQuery] DateOnly weekStart, CancellationToken ct)
        => Ok(await planning.WeekAsync(lotId, weekStart, ct));

    [HttpPost("lots/{lotId:int}/shifts")]
    public async Task<ActionResult<ShiftScheduleDto>> Create(int lotId, [FromBody] CreateShiftsRequest request, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await planning.CreateAsync(lotId, request, ct));

    [HttpPost("lots/{lotId:int}/shifts/copy-week")]
    public async Task<ActionResult<CopyWeekResult>> CopyWeek(int lotId, [FromBody] CopyWeekRequest request, CancellationToken ct)
        => Ok(await planning.CopyWeekAsync(lotId, request, ct));

    [HttpPost("lots/{lotId:int}/shifts/ad-hoc")]
    public async Task<ActionResult<ShiftView>> AdHoc(int lotId, [FromBody] AdHocShiftRequest request, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await planning.AdHocAsync(lotId, request, ct));

    [HttpPut("shifts/{id:int}")]
    public async Task<ActionResult<ShiftView>> Update(int id, [FromBody] SaveShiftRequest request, CancellationToken ct)
        => Ok(await planning.UpdateAsync(id, request, ct));

    [HttpDelete("shifts/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await planning.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("lots/{lotId:int}/handovers")]
    public async Task<ActionResult<IReadOnlyList<ShiftView>>> Handovers(int lotId, [FromQuery] ShiftStatus? status, CancellationToken ct)
        => Ok(await planning.HandoversAsync(lotId, status, ct));

    [HttpPost("shifts/{id:int}/discrepancy/resolve")]
    public async Task<ActionResult<ShiftView>> Resolve(int id, [FromBody] ResolveDiscrepancyRequest request, CancellationToken ct)
        => Ok(await planning.ResolveDiscrepancyAsync(id, request, ct));

    // ===== Người trực =====
    [HttpGet("me/shifts")]
    public async Task<ActionResult<MyShiftsDto>> Mine([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await duty.MineAsync(from?.ToUniversalTime(), to?.ToUniversalTime(), ct));

    [HttpPost("shifts/{id:int}/start")]
    public async Task<ActionResult<ShiftView>> Start(int id, CancellationToken ct) => Ok(await duty.StartAsync(id, ct));

    [HttpGet("shifts/{id:int}/summary")]
    public async Task<ActionResult<ShiftSummaryDto>> Summary(int id, CancellationToken ct) => Ok(await duty.SummaryAsync(id, ct));

    [HttpPost("shifts/{id:int}/end")]
    public async Task<ActionResult<ShiftView>> End(int id, [FromBody] EndShiftRequest request, CancellationToken ct)
        => Ok(await duty.EndAsync(id, request, ct));

    [HttpPost("shifts/{id:int}/handover/accept")]
    public async Task<ActionResult<ShiftView>> Accept(int id, [FromBody] AcceptHandoverRequest request, CancellationToken ct)
        => Ok(await duty.AcceptHandoverAsync(id, request, ct));

    // ===== Admin (chỉ đọc) =====
    [HttpGet("admin/shifts")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<ShiftPageDto>> AdminList([FromQuery] ShiftStatus? status, [FromQuery] int? parkingLotId,
        [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await planning.AdminListAsync(status, parkingLotId, fromUtc?.ToUniversalTime(), toUtc?.ToUniversalTime(), page, pageSize, ct));
}
