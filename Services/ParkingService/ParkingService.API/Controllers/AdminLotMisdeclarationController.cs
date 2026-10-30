using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Contracts;
using ParkingService.Application.Features.KybMisdeclaration;

namespace ParkingService.API.Controllers;

/// <summary>
/// US-056: Admin xử lý bãi khai báo sai sau khi đã Active – cảnh báo kèm hạn sửa, tạm khóa kèm hạn giải trình 24h, khôi phục.
/// Route dưới "parking-lots" để Gateway chuyển sang ParkingService. ParkingService là nơi DUY NHẤT đổi ParkingLot.Status.
/// Chỉ role Admin; người thao tác lấy từ JWT, không nhận từ body.
/// </summary>
[ApiController]
[Route("api/v1/parking-lots/{lotId:int}/misdeclaration-cases")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminLotMisdeclarationController(
    IGetLotMisdeclarationUseCase getMisdeclaration,
    IOpenLotMisdeclarationCaseUseCase openCase,
    IEscalateLotMisdeclarationCaseUseCase escalateCase,
    IResolveLotMisdeclarationCaseUseCase resolveCase,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>GET /api/v1/parking-lots/7/misdeclaration-cases – bãi (kể cả Suspended), case đang mở, lịch sử case.</summary>
    [HttpGet]
    [ProducesResponseType<LotMisdeclarationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LotMisdeclarationDto>> Get(int lotId, CancellationToken cancellationToken)
        => Ok(await getMisdeclaration.ExecuteAsync(lotId, cancellationToken));

    /// <summary>POST …/7/misdeclaration-cases { severity, category, finding, correctionDays? } – cảnh báo (Minor) hoặc tạm khóa (Suspected).</summary>
    [HttpPost]
    [ProducesResponseType<LotMisdeclarationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LotMisdeclarationDto>> Open(int lotId, [FromBody] OpenLotMisdeclarationCaseRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminUserId) return Unauthorized();
        var result = await openCase.ExecuteAsync(lotId, request, adminUserId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { lotId }, result);
    }

    /// <summary>POST …/7/misdeclaration-cases/3/escalate { note } – cảnh báo → tạm khóa, hạn giải trình 24h.</summary>
    [HttpPost("{caseId:int}/escalate")]
    [ProducesResponseType<LotMisdeclarationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<LotMisdeclarationDto>> Escalate(int lotId, int caseId,
        [FromBody] EscalateLotMisdeclarationCaseRequest request, CancellationToken cancellationToken)
        => AsAdmin(adminId => escalateCase.ExecuteAsync(lotId, caseId, request, adminId, cancellationToken));

    /// <summary>POST …/7/misdeclaration-cases/3/resolve { note } – đóng case; bãi đang tạm khóa → khôi phục Active.</summary>
    [HttpPost("{caseId:int}/resolve")]
    [ProducesResponseType<LotMisdeclarationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<LotMisdeclarationDto>> Resolve(int lotId, int caseId,
        [FromBody] ResolveLotMisdeclarationCaseRequest request, CancellationToken cancellationToken)
        => AsAdmin(adminId => resolveCase.ExecuteAsync(lotId, caseId, request, adminId, cancellationToken));

    private async Task<ActionResult<LotMisdeclarationDto>> AsAdmin(Func<int, Task<LotMisdeclarationDto>> action)
        => currentUser.UserId is { } adminUserId ? Ok(await action(adminUserId)) : Unauthorized();
}
