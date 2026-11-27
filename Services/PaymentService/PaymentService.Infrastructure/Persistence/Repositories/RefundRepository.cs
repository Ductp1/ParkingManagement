using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Domain;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Persistence.Repositories;

/// <summary>
/// Lớp cài đặt kho lưu trữ các giao dịch hoàn tiền và truy xuất thanh toán sử dụng EF Core (US-034, US-041).
/// </summary>
public sealed class RefundRepository(PaymentDbContext db) : IRefundRepository
{
    /// <summary>
    /// Tìm kiếm thông tin giao dịch thanh toán theo khóa chính Id.
    /// </summary>
    public async Task<Payment?> GetPaymentByIdAsync(int paymentId, CancellationToken cancellationToken)
    {
        return await db.Payments
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
    }

    /// <summary>
    /// Tìm kiếm bản ghi hoàn tiền theo khóa chính Id (US-041).
    /// </summary>
    public async Task<Refund?> GetRefundByIdAsync(int refundId, CancellationToken cancellationToken)
    {
        return await db.Refunds
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == refundId, cancellationToken);
    }

    /// <summary>
    /// Lưu đồng thời các thay đổi: cập nhật trạng thái đơn thanh toán, thêm bản ghi hoàn tiền mới và đẩy OutboxMessage trong cùng một giao dịch.
    /// </summary>
    public async Task SaveAutoRefundTransactionAsync(
        Payment payment, 
        Refund refund, 
        OutboxMessage? outboxMessage, 
        CancellationToken cancellationToken)
    {
        // Đánh dấu cập nhật thực thể Payment trong DbContext
        db.Payments.Update(payment);

        // Thêm bản ghi hoàn tiền mới vào bảng Refunds
        await db.Refunds.AddAsync(refund, cancellationToken);

        // Thêm thông điệp sự kiện tích hợp vào OutboxMessages nếu có
        if (outboxMessage != null)
        {
            await db.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
        }

        // Lưu tất cả thay đổi xuống PostgreSQL trong một lần Commit duy nhất
        await db.SaveChangesAsync(cancellationToken);
    }
}

