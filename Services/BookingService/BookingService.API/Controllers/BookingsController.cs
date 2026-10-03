using BookingService.Application.Features.Bookings;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Enums;

namespace BookingService.API.Controllers;

[ApiController]
[Route("api/bookings")]
public sealed class BookingsController(
    IGetBookingByCodeUseCase getByCode,
    IListMyBookingsUseCase listMine,
    IPreviewCancellationUseCase previewCancellation) : ControllerBase
{
    /// <summary>GET /api/bookings/BK-0002 – Chi tiết booking kèm giá đã khóa và lịch sử trạng thái.</summary>
    [HttpGet("{code}")]
    public async Task<ActionResult<BookingDetailDto>> GetByCode(string code, CancellationToken cancellationToken)
        => Ok(await getByCode.ExecuteAsync(code, cancellationToken));

    /// <summary>GET /api/bookings?userId=5&amp;status=Confirmed – Booking của tôi (UC-13).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BookingSummaryDto>>> ListMine(
        [FromQuery] int userId, [FromQuery] BookingStatus? status, CancellationToken cancellationToken)
        => Ok(await listMine.ExecuteAsync(userId, status, cancellationToken));

    /// <summary>GET /api/bookings/BK-0002/cancellation-preview – Hủy lúc này được hoàn bao nhiêu (UC-16).</summary>
    [HttpGet("{code}/cancellation-preview")]
    public async Task<ActionResult<CancellationPreviewDto>> PreviewCancellation(string code, CancellationToken cancellationToken)
        => Ok(await previewCancellation.ExecuteAsync(code, cancellationToken));
}
