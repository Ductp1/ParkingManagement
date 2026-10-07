using BookingService.Application.Features.Bookings;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Enums;

namespace BookingService.API.Controllers;

[ApiController]
[Route("api/v1/bookings")]
public sealed class BookingsController(
    IGetBookingByCodeUseCase getByCode,
    IListMyBookingsUseCase listMine,
    IPreviewCancellationUseCase previewCancellation,
    ICreateBookingUseCase createBooking,
    ICancelBookingUseCase cancelBooking,
    IModifyBookingUseCase modifyBooking,
    IExtendBookingUseCase extendBooking,
    IReviewBookingUseCase reviewBooking,
    ILotCancelBookingUseCase lotCancelBooking,
    IGateBookingActionsUseCase gateActions) : ControllerBase
{
    /// <summary>GET /api/v1/bookings/BK-0002 – Chi tiết booking kèm giá đã khóa và lịch sử trạng thái.</summary>
    [HttpGet("{code}")]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> GetByCode(string code, CancellationToken cancellationToken)
        => Ok(await getByCode.ExecuteAsync(code, cancellationToken));

    /// <summary>GET /api/v1/bookings?userId=5&amp;status=Confirmed – Booking của tôi (UC-13).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<BookingSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookingSummaryDto>>> ListMine(
        [FromQuery] int userId, [FromQuery] BookingStatus? status, CancellationToken cancellationToken)
        => Ok(await listMine.ExecuteAsync(userId, status, cancellationToken));

    /// <summary>GET /api/v1/bookings/BK-0002/cancellation-preview – Xem trước hủy lúc này được hoàn bao nhiêu (UC-16).</summary>
    [HttpGet("{code}/cancellation-preview")]
    [ProducesResponseType<CancellationPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CancellationPreviewDto>> PreviewCancellation(string code, CancellationToken cancellationToken)
        => Ok(await previewCancellation.ExecuteAsync(code, cancellationToken));

    /// <summary>POST /api/v1/bookings – Tạo booking và giữ chỗ 15 phút (US-022, US-024, US-025, US-026, US-027).</summary>
    [HttpPost]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BookingDetailDto>> Create([FromBody] CreateBookingCommand command, CancellationToken cancellationToken)
    {
        var result = await createBooking.ExecuteAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetByCode), new { code = result.Code }, result);
    }

    /// <summary>POST /api/v1/bookings/{code}/cancel – Khách hủy booking theo Cancellation Window 60 phút (US-038).</summary>
    [HttpPost("{code}/cancel")]
    [ProducesResponseType<CancellationResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CancellationResultDto>> Cancel(string code, [FromBody] CancelBookingCommand command, CancellationToken cancellationToken)
        => Ok(await cancelBooking.ExecuteAsync(code, command, cancellationToken));

    /// <summary>PUT /api/v1/bookings/{code} – Sửa khung giờ hoặc xe trước Check-in (US-029).</summary>
    [HttpPut("{code}")]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> Modify(string code, [FromBody] ModifyBookingCommand command, CancellationToken cancellationToken)
        => Ok(await modifyBooking.ExecuteAsync(code, command, cancellationToken));

    /// <summary>POST /api/v1/bookings/{code}/extend – Gia hạn thời gian đỗ xe (US-030).</summary>
    [HttpPost("{code}/extend")]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> Extend(string code, [FromBody] ExtendBookingCommand command, CancellationToken cancellationToken)
        => Ok(await extendBooking.ExecuteAsync(code, command, cancellationToken));

    /// <summary>POST /api/v1/bookings/{code}/approve – Chủ bãi duyệt booking (Bãi Mức 0 - US-031).</summary>
    [HttpPost("{code}/approve")]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> Approve(string code, [FromBody] ReviewBookingCommand command, CancellationToken cancellationToken)
        => Ok(await reviewBooking.ApproveAsync(code, command, cancellationToken));

    /// <summary>POST /api/v1/bookings/{code}/reject – Chủ bãi từ chối booking (Bãi Mức 0 - US-031).</summary>
    [HttpPost("{code}/reject")]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> Reject(string code, [FromBody] ReviewBookingCommand command, CancellationToken cancellationToken)
        => Ok(await reviewBooking.RejectAsync(code, command, cancellationToken));

    /// <summary>POST /api/v1/bookings/{code}/lot-cancel – Chủ bãi hủy booking do sự cố bãi (Hoàn 100% - US-042).</summary>
    [HttpPost("{code}/lot-cancel")]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> LotCancel(string code, [FromBody] LotCancelBookingCommand command, CancellationToken cancellationToken)
        => Ok(await lotCancelBooking.ExecuteAsync(code, command, cancellationToken));

    /// <summary>POST /api/v1/bookings/{code}/check-in – Cổng xác nhận xe vào (Nội bộ cho TV7 - GateService).</summary>
    [HttpPost("{code}/check-in")]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> CheckIn(string code, [FromBody] GateActionCommand command, CancellationToken cancellationToken)
        => Ok(await gateActions.CheckInAsync(code, command, cancellationToken));

    /// <summary>POST /api/v1/bookings/{code}/check-out – Cổng xác nhận xe ra (Nội bộ cho TV7 - GateService).</summary>
    [HttpPost("{code}/check-out")]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> CheckOut(string code, [FromBody] GateActionCommand command, CancellationToken cancellationToken)
        => Ok(await gateActions.CheckOutAsync(code, command, cancellationToken));

    /// <summary>POST /api/v1/bookings/{code}/no-show – Cổng đánh dấu No-show sau 30 phút (Nội bộ cho TV7 - GateService).</summary>
    [HttpPost("{code}/no-show")]
    [ProducesResponseType<BookingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDetailDto>> NoShow(string code, [FromBody] GateActionCommand command, CancellationToken cancellationToken)
        => Ok(await gateActions.NoShowAsync(code, command, cancellationToken));
}
