using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Application.Features;
using PaymentService.Application.Features.Pricing;

namespace PaymentService.API.Controllers;

/// <summary>
/// Controller xử lý tính toán và báo giá đỗ xe trước khi đặt chỗ (UC-11, US-046, US-070, US-071) và tính phụ thu quá giờ (US-047, UC-22).
/// </summary>
[ApiController]
[Route("api/v1/pricing")]
public sealed class PricingController(
    IQuotePriceUseCase quotePrice,
    IComputeSessionFeeUseCase sessionFee,
    ICalculateOverstayPenaltyUseCase calculateOverstayPenalty) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/pricing/quote – Tính toán và báo giá tiền đỗ xe trước khi đặt chỗ (UC-11, US-046, US-070, US-071).
    /// </summary>
    /// <param name="parkingLotId">{int} - FE truyền qua Query - Mã định danh của bãi xe.</param>
    /// <param name="vehicleType">{VehicleType} - FE truyền qua Query - Loại xe (Sedan, Suv, Oversized, Motorbike).</param>
    /// <param name="startAtUtc">{DateTime} - FE truyền qua Query - Thời điểm bắt đầu đỗ theo giờ UTC.</param>
    /// <param name="endAtUtc">{DateTime} - FE truyền qua Query - Thời điểm kết thúc đỗ theo giờ UTC.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng PriceQuoteDto chứa thông tin tính giá chi tiết.
    /// Mã HTTP trả về:
    /// - 200 OK: Tính giá thành công.
    /// - 400 BadRequest: Dữ liệu không hợp lệ.
    /// - 404 NotFound: Không tìm thấy bảng giá.
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
    /// </summary>
    /// <param name="query">{SessionFeeQuery} - GateService truyền qua Request Body - Thông tin phiên đỗ để chốt phí.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng SessionFeeDto chứa chi tiết cước phiên đỗ và phụ thu quá giờ.
    /// Mã HTTP trả về:
    /// - 200 OK: Chốt phí thành công.
    /// - 400 BadRequest: Dữ liệu không hợp lệ.
    /// - 404 NotFound: Không tìm thấy bảng giá.
    /// </returns>
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

    /// <summary>
    /// POST /api/v1/pricing/overstay-penalty – Tính toán chi tiết phí phụ thu quá giờ khi xe ra bãi trễ so với giờ đã đặt trước (US-047, UC-22).
    /// </summary>
    /// <param name="query">{CalculateOverstayPenaltyQuery} - FE hoặc GateService truyền qua Request Body - Dữ liệu yêu cầu gồm mã bãi xe, loại xe, giờ kết thúc đặt trước và giờ ra thực tế.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng OverstayPenaltyDto chứa:
    /// - ParkingLotId: Mã định danh của bãi xe.
    /// - RateCardId: Mã định danh bảng giá áp dụng.
    /// - VehicleType: Loại xe (Sedan, SUV, Oversized...).
    /// - OverstayMinutes: Tổng số phút quá giờ thực tế.
    /// - IsWithinGracePeriod: Đánh dấu lượt xe có nằm trong 15 phút ân hạn miễn phạt hay không.
    /// - OverstayMultiplier: Hệ số phạt áp dụng (1.5 đến 2.0).
    /// - BaseRateAmount: Tiền cước tính theo đơn giá thông thường.
    /// - PenaltyAmount: Tổng số tiền phạt thực tế phải thanh toán trước khi mở barrier.
    /// - Description: Chuỗi văn bản mô tả chi tiết công thức để Kiosk hiển thị cho tài xế xem.
    /// - Lines: Danh sách chi tiết các dòng giá theo block.
    /// 
    /// Các mã HTTP Status trả về:
    /// - 200 OK: Tính toán phí phạt quá giờ thành công.
    /// - 400 BadRequest: Dữ liệu gửi lên không hợp lệ (mã bãi xe nhỏ hơn hoặc bằng 0, loại xe không hợp lệ).
    /// - 404 NotFound: Không tìm thấy bảng giá tương ứng của bãi xe.
    /// </returns>
    [HttpPost("overstay-penalty")]
    [ProducesResponseType<OverstayPenaltyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OverstayPenaltyDto>> CalculateOverstayPenalty(
        [FromBody] CalculateOverstayPenaltyQuery query,
        CancellationToken cancellationToken)
    {
        var bookedEnd = query.EffectiveBookedEndAtUtc;
        if (bookedEnd > DateTime.MinValue && bookedEnd.Year >= 2000)
        {
            query = query with
            {
                BookedEndAtUtc = DateTime.SpecifyKind(bookedEnd.ToUniversalTime(), DateTimeKind.Utc)
            };
        }

        if (query.ActualExitAtUtc > DateTime.MinValue && query.ActualExitAtUtc.Year >= 2000)
        {
            query = query with
            {
                ActualExitAtUtc = DateTime.SpecifyKind(query.ActualExitAtUtc.ToUniversalTime(), DateTimeKind.Utc)
            };
        }

        return Ok(await calculateOverstayPenalty.ExecuteAsync(query, cancellationToken));
    }
}

