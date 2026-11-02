using ParkingManagement.SharedKernel.Domain;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Interfaces;

/// <summary>
/// Cổng giao tiếp lưu trữ và quản lý thực thể Payment trong cơ sở dữ liệu pm_payment (US-032, US-033).
/// </summary>
public interface IPaymentRepository
{
    /// <summary>
    /// Thêm mới một bản ghi giao dịch thanh toán vào cơ sở dữ liệu (US-032).
    /// </summary>
    /// <param name="payment">Thực thể thanh toán cần lưu trữ.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    Task AddAsync(Payment payment, CancellationToken cancellationToken);

    /// <summary>
    /// Tìm kiếm thông tin giao dịch thanh toán dựa trên mã thanh toán nội bộ (Code / vnp_TxnRef) (US-033).
    /// </summary>
    /// <param name="paymentCode">Mã giao dịch duy nhất dạng PAY_... cần tra cứu.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    /// <returns>Thực thể Payment nếu tìm thấy, hoặc null nếu không tồn tại.</returns>
    Task<Payment?> GetByCodeAsync(string paymentCode, CancellationToken cancellationToken);

    /// <summary>
    /// Lưu đồng bộ toàn bộ kết quả xử lý callback: cập nhật Payment, ghi PaymentCallbackLog và OutboxMessage (US-033).
    /// </summary>
    /// <param name="payment">Thực thể giao dịch cần cập nhật trạng thái (null nếu không tìm thấy đơn).</param>
    /// <param name="callbackLog">Bản ghi nhật ký lưu vết chi tiết lần gọi của VNPAY.</param>
    /// <param name="outboxMessage">Sự kiện tích hợp PaymentSucceeded/PaymentFailed cần phát (nếu có).</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    Task SaveCallbackResultAsync(
        Payment? payment, 
        PaymentCallbackLog callbackLog, 
        OutboxMessage? outboxMessage, 
        CancellationToken cancellationToken);
}

