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
/// DTO chứa thông tin FE hoặc Kiosk cổng truyền qua Request Body để yêu cầu tạo mã thanh toán VietQR động (Task T-402).
/// </summary>
/// <param name="BookingId">Mã định danh duy nhất của lượt đặt chỗ (nếu có).</param>
/// <param name="BookingCode">Mã hiển thị của lượt đặt chỗ (ví dụ: BK-0002).</param>
/// <param name="ParkingSessionId">Mã định danh phiên đỗ xe thực tế tại cổng (nếu là khách vãng lai hoặc check-out tại cổng).</param>
/// <param name="ParkingLotId">Mã định danh của bãi đỗ xe.</param>
/// <param name="OwnerProfileId">Mã định danh chủ bãi xe thụ hưởng tiền.</param>
/// <param name="UserId">Mã định danh tài xế đặt chỗ (null nếu khách vãng lai).</param>
/// <param name="Amount">Số tiền thực tế cần thanh toán (VNĐ).</param>
/// <param name="BankBin">Mã BIN ngân hàng nhận tiền (6 chữ số chuẩn NAPAS, ví dụ: 970422 - MBBank).</param>
/// <param name="BankAccountNumber">Số tài khoản ngân hàng của chủ bãi xe nhận tiền.</param>
/// <param name="BankAccountName">Tên chủ tài khoản ngân hàng thụ hưởng.</param>
public sealed record CreateVietQrPaymentRequest(
    int? BookingId,
    string? BookingCode,
    int? ParkingSessionId,
    int ParkingLotId,
    int OwnerProfileId,
    int? UserId,
    decimal Amount,
    string BankBin,
    string BankAccountNumber,
    string BankAccountName);

/// <summary>
/// Controller xử lý tính toán và báo giá đỗ xe trước khi đặt chỗ (UC-11, US-046, US-070, US-071).
/// </summary>
[ApiController]
[Route("api/v1/pricing")]
public sealed class PricingController(IQuotePriceUseCase quotePrice) : ControllerBase
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
}

/// <summary>
/// Controller quản lý và tra cứu các giao dịch thanh toán trong hệ thống.
/// </summary>
[ApiController]
[Route("api/v1/payments")]
public sealed class PaymentsController(
    IGetPaymentsByBookingUseCase getByBooking,
    ICreateVnpayPaymentUseCase createVnpayPayment,
    ICreateVietQrPaymentUseCase createVietQrPayment,
    IProcessVnpayReturnUseCase processVnpayReturn,
    IProcessVnpayIpnUseCase processVnpayIpn) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/payments/vnpay – Khởi tạo giao dịch và sinh URL thanh toán online qua VNPAY Sandbox (US-032).
    /// </summary>
    /// <param name="request">{CreateVnpayPaymentRequest} - FE truyền qua Request Body - Dữ liệu yêu cầu thanh toán gồm mã đặt chỗ, số tiền và thông tin bãi xe.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng VnpayPaymentUrlDto chứa các trường dữ liệu:
    /// - PaymentCode: Mã giao dịch nội bộ duy nhất được tạo trong hệ thống.
    /// - PaymentUrl: Đường dẫn chuyển hướng sang cổng thanh toán VNPAY Sandbox.
    /// - ExpiresAtUtc: Thời điểm hết hạn của phiên thanh toán (sau 15 phút).
    /// 
    /// Các mã HTTP Status trả về:
    /// - 200 OK: Khởi tạo giao dịch và tạo URL thanh toán thành công.
    /// - 400 BadRequest: Dữ liệu gửi lên không hợp lệ (số tiền nhỏ hơn hoặc bằng 0, hoặc thiếu mã bookingId).
    /// </returns>
    [HttpPost("vnpay")]
    [ProducesResponseType<VnpayPaymentUrlDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VnpayPaymentUrlDto>> CreateVnpayPayment(
        [FromBody] CreateVnpayPaymentRequest request,
        CancellationToken cancellationToken)
    {
        // Lấy địa chỉ IP mạng thực tế của thiết bị khách hàng (chuyển đổi IPv6 localhost ::1 thành 127.0.0.1 chuẩn IPv4 cho VNPAY)
        var rawIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var clientIp = string.IsNullOrWhiteSpace(rawIp) || rawIp == "::1" || rawIp == "0.0.0.1"
            ? "127.0.0.1"
            : rawIp;

        // Chuyển đổi dữ liệu từ Request Body thành Command cho tầng nghiệp vụ xử lý
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
    /// GET /api/v1/payments/vnpay-return – Tiếp nhận chuyển hướng trình duyệt của khách hàng sau khi thanh toán trên cổng VNPAY (US-033).
    /// </summary>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng VnpayReturnResultDto chứa:
    /// - IsSuccess: Trạng thái thanh toán thành công hay thất bại.
    /// - Message: Thông điệp phản hồi hiển thị lên giao diện.
    /// - PaymentCode: Mã định danh giao dịch nội bộ (PAY_...).
    /// - BookingCode: Mã lượt đặt chỗ gửi xe (ví dụ: BK-0002).
    /// - Amount: Số tiền thực tế thanh toán.
    /// - Status: Trạng thái hiện tại của giao dịch (Succeeded hoặc Failed).
    /// 
    /// Các mã HTTP Status trả về:
    /// - 200 OK: Tiếp nhận và xử lý kết quả chuyển hướng thành công.
    /// </returns>
    [HttpGet("vnpay-return")]
    [ProducesResponseType<VnpayReturnResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<VnpayReturnResultDto>> ProcessVnpayReturn(CancellationToken cancellationToken)
    {
        var queryParams = Request.Query.ToDictionary(k => k.Key, v => v.Value.ToString());
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await processVnpayReturn.ExecuteAsync(queryParams, clientIp, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/v1/payments/vnpay-ipn – Tiếp nhận thông báo kết quả thanh toán tức thì (Webhook IPN) từ máy chủ VNPAY gửi ngầm (US-033).
    /// </summary>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng VnpayIpnResponseDto chứa mã phản hồi quy chuẩn theo tài liệu VNPAY:
    /// - RspCode: "00" (Thành công), "97" (Sai chữ ký), "01" (Không tìm thấy đơn), "04" (Sai số tiền), "02" (Đơn đã xác nhận trước đó).
    /// - Message: Mô tả tương ứng với mã phản hồi.
    /// 
    /// Các mã HTTP Status trả về:
    /// - 200 OK: Luôn trả về 200 kèm body JSON quy chuẩn theo tài liệu kỹ thuật VNPAY.
    /// </returns>
    [HttpGet("vnpay-ipn")]
    [ProducesResponseType<VnpayIpnResponseDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<VnpayIpnResponseDto>> ProcessVnpayIpn(CancellationToken cancellationToken)
    {
        var queryParams = Request.Query.ToDictionary(k => k.Key, v => v.Value.ToString());
        var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await processVnpayIpn.ExecuteAsync(queryParams, sourceIp, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/v1/payments?bookingId=2 – Tra cứu danh sách các lần thanh toán của một lượt đặt chỗ (UC-19).
    /// </summary>
    /// <summary>
    /// POST /api/v1/payments/vietqr – Khởi tạo giao dịch và sinh mã QR động chuẩn VietQR NAPAS247 (Task T-402).
    /// </summary>
    /// <param name="request">{CreateVietQrPaymentRequest} - FE hoặc Kiosk cổng truyền qua Request Body - Dữ liệu yêu cầu thanh toán gồm số tiền, mã lượt đỗ và thông tin tài khoản ngân hàng chủ bãi.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng VietQrPaymentDto chứa các trường dữ liệu:
    /// - PaymentCode: Mã giao dịch nội bộ duy nhất được tạo trong hệ thống.
    /// - QrImageUrl: Đường dẫn hình ảnh mã QR động chuẩn QuickLink để hiển thị lên Kiosk hoặc Web.
    /// - BankBin: Mã BIN ngân hàng nhận tiền.
    /// - BankAccountNumber: Số tài khoản ngân hàng của chủ bãi xe.
    /// - BankAccountName: Tên chủ tài khoản ngân hàng.
    /// - Amount: Số tiền cần thanh toán (VNĐ).
    /// - TransferContent: Nội dung chuyển khoản tự động điền sẵn trên ứng dụng ngân hàng.
    /// - ExpiresAtUtc: Thời điểm hết hạn của phiên thanh toán (sau 15 phút).
    /// 
    /// Các mã HTTP Status trả về:
    /// - 200 OK: Tạo mã VietQR thành công.
    /// - 400 BadRequest: Dữ liệu gửi lên không hợp lệ (số tiền nhỏ hơn hoặc bằng 0, thiếu số tài khoản hoặc thiếu mã booking/lượt đỗ).
    /// </returns>
    [HttpPost("vietqr")]
    [ProducesResponseType<VietQrPaymentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VietQrPaymentDto>> CreateVietQrPayment(
        [FromBody] CreateVietQrPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateVietQrPaymentCommand(
            request.BookingId,
            request.BookingCode,
            request.ParkingSessionId,
            request.ParkingLotId,
            request.OwnerProfileId,
            request.UserId,
            request.Amount,
            request.BankBin,
            request.BankAccountNumber,
            request.BankAccountName);

        var result = await createVietQrPayment.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/v1/payments?bookingId={bookingId} – Tra cứu toàn bộ lịch sử các giao dịch thanh toán của một lượt đặt chỗ (UC-12).
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
}

