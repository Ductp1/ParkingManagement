using GateService.Application.Features;
using GateService.Application.Features.GateConsole;
using Microsoft.AspNetCore.Mvc;

namespace GateService.API.Controllers;

/// <summary>
/// T-702/UC-20/UC-22: API của Gate Console – cổng vào/ra + danh sách booking trong ca.
/// Controller chỉ điều hướng HTTP → Use case, không chứa nghiệp vụ.
/// </summary>
[ApiController]
[Route("api/v1/gate")]
public sealed class GateController(ICheckInUseCase checkIn, ICheckOutUseCase checkOut,
    IQuoteExitFeeUseCase quoteExitFee, IListShiftBookingsUseCase listShiftBookings) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/gate/check-in – Check-in tại cổng vào (UC-21, US-043/044).
    /// Body: { parkingLotId, method: "Qr" | "BookingCode" | "Manual", plate, bookingCode?, staffUserId?, qrToken? }.
    /// method = "Qr" (T-704) bắt buộc qrToken hợp lệ do BookingService cấp; khách vãng lai = không kèm bookingCode.
    /// Booking phải Confirmed (BookingService kiểm tra); đến trễ ≤ 15' vẫn vào bình thường (ân hạn US-044).
    /// </summary>
    [HttpPost("check-in")]
    public async Task<ActionResult<ParkingSessionDto>> CheckIn([FromBody] CheckInCommand command, CancellationToken cancellationToken)
        => Ok(await checkIn.ExecuteAsync(command, cancellationToken));

    /// <summary>
    /// GET /api/v1/gate/exit-quote?parkingLotId=1&amp;plate=51F-123.45 – UC-22 bước 1–3 (US-045).
    /// Tra xe đang đỗ và tính tổng tiền + phụ thu quá giờ để staff xác nhận trước khi mở barie.
    /// </summary>
    [HttpGet("exit-quote")]
    [ProducesResponseType<ExitFeeQuoteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExitFeeQuoteDto>> ExitQuote(
        [FromQuery] int parkingLotId, [FromQuery] string plate, CancellationToken cancellationToken)
        => Ok(await quoteExitFee.ExecuteAsync(parkingLotId, plate, cancellationToken));

    /// <summary>
    /// POST /api/v1/gate/check-out – Check-out tại cổng ra (T-702 + UC-22).
    /// Body: { parkingLotId, plate, method: "Qr" | "Manual" (mặc định Manual), staffUserId?, confirmFee? }.
    /// confirmFee = true → chốt phí (Billing Grace 15' + overstay × hệ số), ghi thu tiền phụ thu (Payment),
    /// đồng bộ booking Completed; kết quả trả CheckOutResultDto (session + fee + warning nếu có).
    /// </summary>
    [HttpPost("check-out")]
    public async Task<ActionResult<CheckOutResultDto>> CheckOut([FromBody] CheckOutCommand command, CancellationToken cancellationToken)
        => Ok(await checkOut.ExecuteAsync(command, cancellationToken));

    /// <summary>
    /// GET /api/v1/gate/console/bookings?parkingLotId=1&amp;staffUserId=4&amp;search=BK-0002 – UC-20 (US-076).
    /// Booking trong ca trực của staff (khung giờ theo ca đang mở), kèm khách vãng lai đang đỗ; không trả SĐT/CCCD.
    /// </summary>
    [HttpGet("console/bookings")]
    [ProducesResponseType<IReadOnlyList<ShiftBookingItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ShiftBookingItemDto>>> ConsoleBookings(
        [FromQuery] int? parkingLotId, [FromQuery] int? staffUserId, [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc, [FromQuery] string? search, CancellationToken cancellationToken)
        => Ok(await listShiftBookings.ExecuteAsync(
            new ShiftBookingsQuery(parkingLotId, staffUserId, fromUtc, toUtc, search), cancellationToken));
}
