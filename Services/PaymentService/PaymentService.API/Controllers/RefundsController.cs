using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Features.Refunds;

namespace PaymentService.API.Controllers;

/// <summary>
/// Controller xử lý các nghiệp vụ hoàn tiền trong hệ thống (US-034: Tự động hoàn tiền khi booking thất bại, US-041: Chủ bãi hoàn tiền do sự cố).
/// </summary>
[ApiController]
[Route("api/v1/refunds")]
public sealed class RefundsController(IProcessAutoRefundUseCase autoRefundUseCase) : ControllerBase
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
}

