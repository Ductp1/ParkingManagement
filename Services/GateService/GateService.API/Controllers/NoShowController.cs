using GateService.Application.Features.GateConsole;
using Microsoft.AspNetCore.Mvc;

namespace GateService.API.Controllers;

/// <summary>
/// US-039 / UC-24: Staff hủy booking No-show sau 30 phút quá hạn check-in.
/// Controller chỉ điều hướng HTTP → Use case, không chứa nghiệp vụ.
/// </summary>
[ApiController]
[Route("api/v1/gate/no-show")]
public sealed class NoShowController(IListNoShowCandidatesUseCase listCandidates, ICancelNoShowUseCase cancelNoShow) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/gate/no-show/candidates?parkingLotId=2&amp;graceMinutes=30 – UC-24 bước 1:
    /// booking Confirmed chưa check-in đã quá mốc ân hạn (mặc định 30 phút) để Staff xem và xử lý.
    /// </summary>
    [HttpGet("candidates")]
    [ProducesResponseType<IReadOnlyList<GateNoShowCandidate>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<GateNoShowCandidate>>> Candidates(
        [FromQuery] int parkingLotId, [FromQuery] int graceMinutes = 0, CancellationToken cancellationToken = default)
        => Ok(await listCandidates.ExecuteAsync(parkingLotId, graceMinutes, cancellationToken));

    /// <summary>
    /// POST /api/v1/gate/no-show/{bookingCode}/cancel – UC-24 bước 3–5.
    /// Body: { parkingLotId, staffUserId, reason? }. Trước mốc 30 phút → 400; đã check-in / sai trạng thái → 409.
    /// Thành công: booking → NoShow, nhả slot đã giữ, ghi GateEvents (NoShowCancel) + bộ đếm ca trực.
    /// </summary>
    [HttpPost("{bookingCode}/cancel")]
    [ProducesResponseType<NoShowCancelResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<NoShowCancelResultDto>> Cancel(
        string bookingCode, [FromBody] CancelNoShowCommand command, CancellationToken cancellationToken)
        => Ok(await cancelNoShow.ExecuteAsync(bookingCode, command, cancellationToken));
}
