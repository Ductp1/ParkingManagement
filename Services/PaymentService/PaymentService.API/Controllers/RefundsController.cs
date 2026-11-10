using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Exceptions;
using PaymentService.Application.Features.Refunds;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.API.Controllers;

/// <summary>
/// Controller xử lý các nghiệp vụ hoàn tiền trong hệ thống (US-034: Tự động hoàn tiền khi booking thất bại, US-041: Chủ bãi hoàn tiền do sự cố).
/// </summary>
[ApiController]
[Route("api/v1/refunds")]
public sealed class RefundsController(
    IProcessAutoRefundUseCase autoRefundUseCase,
    IProcessOwnerRefundUseCase ownerRefundUseCase,
    IRefundRepository refundRepository) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/refunds/auto-refund – Xử lý tự động hoàn tiền 100% khi đã trừ tiền nhưng kích hoạt booking thất bại (US-034).
    /// </summary>
    /// <param name="command">{ProcessAutoRefundCommand} - FE hoặc BookingService truyền qua Request Body - Dữ liệu yêu cầu hoàn tiền tự động gồm PaymentId, IsLotFull và lý do lỗi.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng AutoRefundResultDto chứa:
    /// - PaymentId: Mã định danh giao dịch thanh toán.
    /// - RefundId: Mã bản ghi hoàn tiền (null nếu bãi còn chỗ và chọn thử lại).
    /// - RefundAmount: Số tiền được hoàn (VNĐ).
    /// - ActionTaken: Hành động đã thực hiện (Refunded100Percent hoặc RetryNewBooking).
    /// - Status: Trạng thái hoàn tiền (Completed hoặc Retrying).
    /// - Message: Thông điệp phản hồi chi tiết.
    /// 
    /// Các mã HTTP Status trả về:
    /// - 200 OK: Xử lý hoàn tất thành công (hoàn tiền hoặc kích hoạt tạo booking mới).
    /// - 400 BadRequest: Dữ liệu đầu vào không hợp lệ hoặc giao dịch chưa thành công / đã hoàn đủ tiền.
    /// - 404 NotFound: Không tìm thấy giao dịch thanh toán tương ứng với PaymentId.
    /// </returns>
    [HttpPost("auto-refund")]
    [ProducesResponseType<AutoRefundResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AutoRefundResultDto>> ProcessAutoRefund(
        [FromBody] ProcessAutoRefundCommand command,
        CancellationToken cancellationToken)
    {
        var result = await autoRefundUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/v1/refunds/owner-refund – Chủ bãi xe chủ động hoàn tiền bồi thường cho khách do sự cố tại bãi (US-041, UC-36).
    /// </summary>
    /// <param name="command">{ProcessOwnerRefundCommand} - Chủ bãi truyền qua Request Body - Thông tin hoàn tiền gồm PaymentId, OwnerProfileId, Amount, ReasonDescription và ComplaintId nếu có.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Đối tượng OwnerRefundResultDto chứa:
    /// - PaymentId: Mã giao dịch thanh toán.
    /// - RefundId: Mã bản ghi hoàn tiền vừa tạo.
    /// - RefundAmount: Số tiền đã hoàn bồi thường (VNĐ).
    /// - Status: Trạng thái hoàn tiền (Completed).
    /// - Reason: Lý do hoàn tiền (LotFault).
    /// - Message: Thông điệp phản hồi chi tiết.
    /// 
    /// Các mã HTTP Status trả về:
    /// - 200 OK: Chủ bãi hoàn tiền bồi thường thành công.
    /// - 400 BadRequest: Dữ liệu không hợp lệ (sai chủ bãi, số tiền vượt quá hạn mức còn lại, đơn chưa hoàn tất).
    /// - 404 NotFound: Không tìm thấy giao dịch thanh toán với PaymentId tương ứng.
    /// </returns>
    [HttpPost("owner-refund")]
    [ProducesResponseType<OwnerRefundResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OwnerRefundResultDto>> ProcessOwnerRefund(
        [FromBody] ProcessOwnerRefundCommand command,
        CancellationToken cancellationToken)
    {
        var result = await ownerRefundUseCase.ExecuteAsync(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/v1/refunds/{id} – Tra cứu chi tiết bản ghi hoàn tiền theo mã ID (US-034, US-041, UC-19).
    /// </summary>
    /// <param name="id">{int} - FE truyền qua Route - Mã định danh duy nhất của bản ghi hoàn tiền.</param>
    /// <param name="cancellationToken">{CancellationToken} - Hệ thống tự động truyền - Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>
    /// Bản ghi Refund chi tiết hoặc lỗi nếu không tìm thấy.
    /// Các mã HTTP Status trả về:
    /// - 200 OK: Tìm thấy thông tin bản ghi hoàn tiền.
    /// - 400 BadRequest: Mã ID không hợp lệ (nhỏ hơn hoặc bằng 0).
    /// - 404 NotFound: Không tìm thấy bản ghi hoàn tiền tương ứng.
    /// </returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType<Refund>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Refund>> GetById(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            throw new ValidationException("Mã bản ghi hoàn tiền không hợp lệ.");

        var refund = await refundRepository.GetRefundByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Bản ghi hoàn tiền", id);

        return Ok(refund);
    }
}
