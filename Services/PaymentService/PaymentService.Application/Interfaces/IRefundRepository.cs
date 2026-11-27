using ParkingManagement.SharedKernel.Domain;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Interfaces;

/// <summary>
/// Cổng lưu trữ và truy vấn các bản ghi hoàn tiền (Refund) và giao dịch thanh toán liên quan (US-034, US-041).
/// </summary>
public interface IRefundRepository
{
    /// <summary>
    /// Tìm kiếm thông tin đơn thanh toán theo mã định danh duy nhất (Id).
    /// </summary>
    Task<Payment?> GetPaymentByIdAsync(int paymentId, CancellationToken cancellationToken);

    /// <summary>
    /// Tìm kiếm bản ghi hoàn tiền theo mã định danh duy nhất (Id) - US-041.
    /// </summary>
    Task<Refund?> GetRefundByIdAsync(int refundId, CancellationToken cancellationToken);

    /// <summary>
    /// Lưu nguyên khối bản ghi hoàn tiền mới, cập nhật trạng thái Payment và OutboxMessage trong cùng một giao dịch.
    /// </summary>
    Task SaveAutoRefundTransactionAsync(Payment payment, Refund refund, OutboxMessage? outboxMessage, CancellationToken cancellationToken);
}

