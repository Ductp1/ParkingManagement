using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Features.Invoices;

namespace PaymentService.API.Controllers;

/// <summary>
/// Controller quản lý và phát hành hóa đơn điện tử phí đỗ xe và hoa hồng sàn (US-036).
/// </summary>
[ApiController]
[Route("api/v1/invoices")]
public sealed class InvoicesController(
    IIssueParkingInvoiceUseCase issueParkingUseCase,
    IIssueCommissionInvoiceUseCase issueCommissionUseCase,
    IGetInvoiceUseCase getInvoiceUseCase) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/invoices/parking – Phát hành hóa đơn điện tử phí gửi xe cho tài xế (US-036 AC1, AC2).
    /// </summary>
    /// <param name="command">{IssueParkingInvoiceCommand} - FE truyền qua Request Body - Thông tin giao dịch thanh toán, người mua và thuế suất VAT.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng InvoiceDto chứa chi tiết số hóa đơn, bóc tách tiền trước thuế, VAT và link PDF.
    /// Mã HTTP trả về:
    /// - 201 Created: Phát hành hóa đơn thành công.
    /// - 400 BadRequest: Dữ liệu không hợp lệ (giao dịch chưa thanh toán thành công, đã xuất hóa đơn rồi, thuế suất sai).
    /// - 404 NotFound: Không tìm thấy giao dịch thanh toán tương ứng.
    /// </returns>
    [HttpPost("parking")]
    [ProducesResponseType<InvoiceDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceDto>> IssueParkingInvoice(
        [FromBody] IssueParkingInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        var result = await issueParkingUseCase.ExecuteAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// POST /api/v1/invoices/commission – Phát hành hóa đơn hoa hồng sàn cho chủ bãi xe theo kỳ quyết toán (US-036).
    /// </summary>
    /// <param name="command">{IssueCommissionInvoiceCommand} - FE truyền qua Request Body - Thông tin kỳ quyết toán, doanh nghiệp chủ bãi và thuế VAT.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng InvoiceDto chứa chi tiết hóa đơn hoa hồng.
    /// Mã HTTP trả về:
    /// - 201 Created: Phát hành hóa đơn hoa hồng thành công.
    /// - 400 BadRequest: Kỳ quyết toán chưa được duyệt, không có hoa hồng, hoặc đã xuất hóa đơn rồi.
    /// - 404 NotFound: Không tìm thấy kỳ quyết toán tương ứng.
    /// </returns>
    [HttpPost("commission")]
    [ProducesResponseType<InvoiceDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceDto>> IssueCommissionInvoice(
        [FromBody] IssueCommissionInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        var result = await issueCommissionUseCase.ExecuteAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// GET /api/v1/invoices/{id} – Xem thông tin chi tiết một hóa đơn điện tử theo ID (US-036 AC3).
    /// </summary>
    /// <param name="id">{int} - FE truyền qua Route - Mã định danh duy nhất của hóa đơn.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng InvoiceDto chứa đầy đủ số hóa đơn, ngày phát hành, tiền trước thuế, VAT và link PDF.
    /// Mã HTTP trả về:
    /// - 200 OK: Tìm thấy hóa đơn.
    /// - 400 BadRequest: ID không hợp lệ (nhỏ hơn hoặc bằng 0).
    /// - 404 NotFound: Không tìm thấy hóa đơn với ID tương ứng.
    /// </returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType<InvoiceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceDto>> GetById(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var result = await getInvoiceUseCase.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/v1/invoices/by-payment/{paymentId} – Tra cứu hóa đơn điện tử theo mã giao dịch thanh toán (US-036 AC3).
    /// </summary>
    /// <param name="paymentId">{int} - FE truyền qua Route - Mã định danh của giao dịch thanh toán.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng InvoiceDto chứa chi tiết hóa đơn gắn với giao dịch thanh toán đó.
    /// Mã HTTP trả về:
    /// - 200 OK: Tìm thấy hóa đơn.
    /// - 404 NotFound: Giao dịch thanh toán chưa được xuất hóa đơn.
    /// </returns>
    [HttpGet("by-payment/{paymentId:int}")]
    [ProducesResponseType<InvoiceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceDto>> GetByPaymentId(
        [FromRoute] int paymentId,
        CancellationToken cancellationToken)
    {
        var result = await getInvoiceUseCase.GetByPaymentIdAsync(paymentId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/v1/invoices/by-booking/{bookingId} – Tra cứu hóa đơn điện tử theo mã đơn đặt chỗ (US-036 AC3, UC-19).
    /// </summary>
    /// <param name="bookingId">{int} - FE truyền qua Route - Mã định danh của đơn đặt chỗ.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng InvoiceDto để tài xế xem và tải hóa đơn trong lịch sử chuyến đỗ xe.
    /// Mã HTTP trả về:
    /// - 200 OK: Tìm thấy hóa đơn.
    /// - 404 NotFound: Đơn đặt chỗ này chưa có hóa đơn điện tử.
    /// </returns>
    [HttpGet("by-booking/{bookingId:int}")]
    [ProducesResponseType<InvoiceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceDto>> GetByBookingId(
        [FromRoute] int bookingId,
        CancellationToken cancellationToken)
    {
        var result = await getInvoiceUseCase.GetByBookingIdAsync(bookingId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/v1/invoices/by-settlement/{settlementId} – Tra cứu hóa đơn hoa hồng sàn theo kỳ quyết toán (US-036).
    /// </summary>
    /// <param name="settlementId">{int} - FE truyền qua Route - Mã định danh của kỳ quyết toán doanh thu.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng InvoiceDto của hóa đơn hoa hồng.
    /// Mã HTTP trả về:
    /// - 200 OK: Tìm thấy hóa đơn.
    /// - 404 NotFound: Kỳ quyết toán này chưa được xuất hóa đơn hoa hồng.
    /// </returns>
    [HttpGet("by-settlement/{settlementId:int}")]
    [ProducesResponseType<InvoiceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceDto>> GetBySettlementId(
        [FromRoute] int settlementId,
        CancellationToken cancellationToken)
    {
        var result = await getInvoiceUseCase.GetBySettlementIdAsync(settlementId, cancellationToken);
        return Ok(result);
    }
}

