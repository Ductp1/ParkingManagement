using PaymentService.Domain.Entities;

namespace PaymentService.Application.Interfaces;

/// <summary>
/// Cổng giao tiếp lưu trữ và quản lý thực thể Payment trong cơ sở dữ liệu pm_payment.
/// </summary>
public interface IPaymentRepository
{
    /// <summary>
    /// Thêm mới một bản ghi giao dịch thanh toán vào cơ sở dữ liệu.
    /// </summary>
    /// <param name="payment">Thực thể thanh toán cần lưu trữ.</param>
    /// <param name="cancellationToken">Token hủy tác vụ bất đồng bộ khi client ngắt kết nối.</param>
    Task AddAsync(Payment payment, CancellationToken cancellationToken);
}
