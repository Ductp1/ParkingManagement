using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features;

namespace PaymentService.API.Controllers;

[ApiController]
[Route("api/pricing")]
public sealed class PricingController(IQuotePriceUseCase quotePrice) : ControllerBase
{
    /// <summary>
    /// GET /api/pricing/quote?parkingLotId=1&amp;vehicleType=Sedan&amp;startAtUtc=2026-10-03T02:00:00Z&amp;endAtUtc=2026-10-03T05:00:00Z
    /// – Báo giá trước khi đặt (UC-11).
    /// </summary>
    [HttpGet("quote")]
    public async Task<ActionResult<PriceQuoteDto>> Quote(
        [FromQuery] int parkingLotId, [FromQuery] VehicleType vehicleType, [FromQuery] DateTime startAtUtc, [FromQuery] DateTime endAtUtc,
        CancellationToken cancellationToken)
        => Ok(await quotePrice.ExecuteAsync(new QuotePriceQuery(parkingLotId, vehicleType,
            DateTime.SpecifyKind(startAtUtc.ToUniversalTime(), DateTimeKind.Utc),
            DateTime.SpecifyKind(endAtUtc.ToUniversalTime(), DateTimeKind.Utc)), cancellationToken));
}

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(IGetPaymentsByBookingUseCase getByBooking) : ControllerBase
{
    /// <summary>GET /api/payments?bookingId=2 – Các lần thanh toán của 1 booking (UC-19).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> ListByBooking([FromQuery] int bookingId, CancellationToken cancellationToken)
        => Ok(await getByBooking.ExecuteAsync(bookingId, cancellationToken));
}
