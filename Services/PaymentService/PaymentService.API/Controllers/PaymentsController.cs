using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features;
using PaymentService.Application.Features.Payments;

namespace PaymentService.API.Controllers;

/// <summary>
/// DTO chứa thông tin FE truyền qua Request Body để yêu cầu khởi tạo thanh toán VNPAY (US-032).
/// </summary>
/// <param name="BookingId">Mã định danh duy nhất của lượt đặt chỗ.</param>
/// <param name="BookingCode">Mã hiển thị của lượt đặt chỗ (ví dụ: BK-0002).</param>
/// <param name="ParkingLotId">Mã định danh của bãi đỗ xe.</param>
/// <param name="OwnerProfileId">Mã định danh chủ bãi xe thụ hưởng tiền.</param>
/// <param name="UserId">Mã định danh tài xế đặt chỗ (null nếu khách vãng lai).</param>
/// <param name="Amount">Số tiền thực tế cần thanh toán (VNĐ).</param>
public sealed record CreateVnpayPaymentRequest(
    int BookingId,
    string BookingCode,
    int ParkingLotId,
    int OwnerProfileId,
    int? UserId,
    decimal Amount);

/// <summary>
/// Controller xử lý tính toán và báo giá đỗ xe trước khi đặt chỗ.
/// </summary>
[ApiController]
[Route("api/v1/pricing")]
public sealed class PricingController(
    IQuotePriceUseCase quotePrice,
    IComputeSessionFeeUseCase sessionFee) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/pricing/quote – Tính toán và báo giá tiền đỗ xe trước khi đặt chỗ (UC-11, US-046, US-070, US-071).
    /// </summary>
    [HttpGet("quote")]
    [ProducesResponseType<PriceQuoteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PriceQuoteDto>> Quote(
        [FromQuery] int parkingLotId,
        [FromQuery] VehicleType vehicleType,
        [FromQuery] DateTime startAtUtc,
        [FromQuery] DateTime endAtUtc,
        CancellationToken cancellationToken)
        => Ok(await quotePrice.ExecuteAsync(new QuotePriceQuery(
            parkingLotId,
            vehicleType,
            DateTime.SpecifyKind(startAtUtc.ToUniversalTime(), DateTimeKind.Utc),
            DateTime.SpecifyKind(endAtUtc.ToUniversalTime(), DateTimeKind.Utc)),
            cancellationToken));

    /// <summary>
    /// POST /api/v1/pricing/session-fee – Chốt phí một lượt gửi xe thực tế tại cổng ra (UC-22, US-045 → US-047).
    /// </summary>
    [HttpPost("session-fee")]
    [ProducesResponseType<SessionFeeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionFeeDto>> SessionFee(
        [FromBody] SessionFeeQuery query,
        CancellationToken cancellationToken)
    {
        query = query with
        {
            EntryAtUtc = DateTime.SpecifyKind(query.EntryAtUtc.ToUniversalTime(), DateTimeKind.Utc),
            ExitAtUtc = DateTime.SpecifyKind(query.ExitAtUtc.ToUniversalTime(), DateTimeKind.Utc),
            BookedEndAtUtc = query.BookedEndAtUtc is null
                ? null
                : DateTime.SpecifyKind(query.BookedEndAtUtc.Value.ToUniversalTime(), DateTimeKind.Utc),
        };

        return Ok(await sessionFee.ExecuteAsync(query, cancellationToken));
    }
}

/// <summary>
/// Controller quản lý và tra cứu các giao dịch thanh toán trong hệ thống.
/// </summary>
[ApiController]
[Route("api/v1/payments")]
public sealed class PaymentsController(
    IGetPaymentsByBookingUseCase getByBooking,
    ICreateVnpayPaymentUseCase createVnpayPayment,
    IRecordGateSettlementUseCase gateSettlement) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/payments/vnpay – Khởi tạo giao dịch và sinh URL thanh toán online qua VNPAY Sandbox (US-032).
    /// </summary>
    [HttpPost("vnpay")]
    [ProducesResponseType<VnpayPaymentUrlDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VnpayPaymentUrlDto>> CreateVnpayPayment(
        [FromBody] CreateVnpayPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var rawIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var clientIp = string.IsNullOrWhiteSpace(rawIp)
            || rawIp == "::1"
            || rawIp == "0.0.1"
            ? "127.0.0.1"
            : rawIp;

        var command = new CreateVnpayPaymentCommand(
            request.BookingId,
            request.BookingCode,
            request.ParkingLotId,
            request.OwnerProfileId,
            request.UserId,
            request.Amount,
            clientIp);

        var result = await createVnpayPayment.ExecuteAsync(command, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// GET /api/v1/payments?bookingId=2 – Tra cứu lịch sử thanh toán của một lượt đặt chỗ (UC-19).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PaymentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> ListByBooking(
        [FromQuery] int bookingId,
        CancellationToken cancellationToken)
        => Ok(await getByBooking.ExecuteAsync(bookingId, cancellationToken));

    /// <summary>
    /// POST /api/v1/payments/gate-settlement – Ghi nhận thanh toán tại cổng do staff xác nhận (UC-22 A2, UC-23, US-048).
    /// </summary>
    [HttpPost("gate-settlement")]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentDto>> GateSettlement(
        [FromBody] GateSettlementCommand command,
        CancellationToken cancellationToken)
        => Ok(await gateSettlement.ExecuteAsync(command, cancellationToken));
}