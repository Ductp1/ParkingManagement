using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features;

namespace PaymentService.API.Controllers;

/// <summary>
/// Controller xử lý tính toán và báo giá đỗ xe trước khi đặt chỗ (UC-11, US-046, US-070, US-071).
/// </summary>
[ApiController]
[Route("api/v1/pricing")]
public sealed class PricingController(IQuotePriceUseCase quotePrice, IComputeSessionFeeUseCase sessionFee) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/pricing/quote – Tính toán và báo giá tiền đỗ xe trước khi đặt chỗ (UC-11, US-046, US-070, US-071).
    /// Áp dụng đầy đủ: ân hạn 15 phút miễn phí, bậc giá lũy tiến, phụ thu qua đêm 22:00–06:00, hệ số ngày lễ, cuối tuần, và xe quá khổ.
    /// </summary>
    /// <param name="parkingLotId">{int} - FE truyền qua Query - Mã định danh duy nhất của bãi đỗ xe cần tính giá.</param>
    /// <param name="vehicleType">{VehicleType} - FE truyền qua Query - Phân loại phương tiện gửi xe (Sedan, Suv, Motorbike, Oversized).</param>
    /// <param name="startAtUtc">{DateTime} - FE truyền qua Query - Thời điểm bắt đầu đỗ xe theo chuẩn giờ quốc tế UTC.</param>
    /// <param name="endAtUtc">{DateTime} - FE truyền qua Query - Thời điểm dự kiến kết thúc đỗ xe theo chuẩn giờ quốc tế UTC.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng PriceQuoteDto chứa: ParkingLotId, RateCardId, VehicleType, StartAtUtc, EndAtUtc, DurationMinutes, BaseAmount, Multiplier, FinalAmount, IsWithinGracePeriod, và danh sách chi tiết các bậc giá Lines.
    /// Mã HTTP trả về:
    /// - 200 OK: Tính toán báo giá thành công.
    /// - 400 BadRequest: Dữ liệu gửi lên không hợp lệ (mã bãi xe âm, giờ kết thúc trước giờ bắt đầu, hoặc thời gian gửi vượt quá 30 ngày).
    /// - 404 NotFound: Không tìm thấy bảng giá đang có hiệu lực của bãi xe tại thời điểm bắt đầu đỗ.
    /// </returns>
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
    /// Áp Billing Grace 15' (miễn phí nếu vào ra trong 15 phút – US-046), giá bậc lũy tiến, phụ thu qua đêm,
    /// hệ số lễ/cuối tuần, và phụ thu quá giờ × hệ số Overstay từ giờ kết thúc đã đặt (US-047).
    /// BookingService/GateService truyền BookedEndAtUtc + BillingGracePeriodMinutes + OverstayMultiplier
    /// từ PriceSnapshot để giữ Price Lock; khách vãng lai bỏ trống các trường này.
    /// </summary>
    /// <returns>
    /// - 200 OK: SessionFeeDto gồm DurationMinutes, IsWithinBillingGrace, BaseAmount, OverstayMinutes/OverstayAmount, FinalAmount, Lines.
    /// - 400 BadRequest: dữ liệu không hợp lệ (lotId ≤ 0, giờ ra trước giờ vào, loại xe lạ).
    /// - 404 NotFound: bãi chưa có bảng giá hiệu lực tại thời điểm vào.
    /// </returns>
    [HttpPost("session-fee")]
    [ProducesResponseType<SessionFeeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionFeeDto>> SessionFee([FromBody] SessionFeeQuery query, CancellationToken cancellationToken)
    {
        query = query with
        {
            EntryAtUtc = DateTime.SpecifyKind(query.EntryAtUtc.ToUniversalTime(), DateTimeKind.Utc),
            ExitAtUtc = DateTime.SpecifyKind(query.ExitAtUtc.ToUniversalTime(), DateTimeKind.Utc),
            BookedEndAtUtc = query.BookedEndAtUtc is null ? null : DateTime.SpecifyKind(query.BookedEndAtUtc.Value.ToUniversalTime(), DateTimeKind.Utc),
        };
        return Ok(await sessionFee.ExecuteAsync(query, cancellationToken));
    }
}

/// <summary>
/// Controller quản lý và tra cứu các giao dịch thanh toán trong hệ thống.
/// </summary>
[ApiController]
[Route("api/v1/payments")]
public sealed class PaymentsController(IGetPaymentsByBookingUseCase getByBooking, IRecordGateSettlementUseCase gateSettlement) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/payments?bookingId=2 – Tra cứu danh sách các lần thanh toán của một lượt đặt chỗ (UC-19).
    /// </summary>
    /// <param name="bookingId">{int} - FE truyền qua Query - Mã định danh của lượt đặt chỗ cần xem lịch sử thanh toán.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Danh sách các đối tượng PaymentDto chứa chi tiết từng lần thanh toán.
    /// Mã HTTP trả về:
    /// - 200 OK: Tra cứu thành công danh sách giao dịch.
    /// - 400 BadRequest: Mã bookingId không hợp lệ (nhỏ hơn hoặc bằng 0).
    /// </returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PaymentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> ListByBooking(
        [FromQuery] int bookingId, 
        CancellationToken cancellationToken)
        => Ok(await getByBooking.ExecuteAsync(bookingId, cancellationToken));

    /// <summary>
    /// POST /api/v1/payments/gate-settlement – Ghi nhận thanh toán tại cổng do staff xác nhận (UC-22 A2, UC-23, US-048).
    /// MVP: tiền mặt / VietQR tại cổng → Payment Status = Succeeded, Purpose = Overstay (có booking) hoặc WalkIn (vãng lai).
    /// Idempotent theo SessionCode: gọi lặp không tạo giao dịch trùng (unique index IdempotencyKey).
    /// Gọi từ GateService khi staff xác nhận số tiền hiển thị ở bước exit-quote.
    /// </summary>
    /// <returns>
    /// - 200 OK: PaymentDto (mã giao dịch, số tiền, trạng thái Succeeded).
    /// - 400 BadRequest: dữ liệu không hợp lệ (sessionCode trống, số tiền âm, phương thức lạ).
    /// </returns>
    [HttpPost("gate-settlement")]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentDto>> GateSettlement([FromBody] GateSettlementCommand command, CancellationToken cancellationToken)
        => Ok(await gateSettlement.ExecuteAsync(command, cancellationToken));
}
