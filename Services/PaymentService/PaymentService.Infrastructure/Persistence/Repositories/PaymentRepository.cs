using Microsoft.EntityFrameworkCore;
using ParkingManagement.SharedKernel.Domain;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Persistence.Repositories;

/// <summary>
/// Lớp cài đặt kho lưu trữ thực thể Payment sử dụng EF Core và DbContext riêng pm_payment.
/// </summary>
public sealed class PaymentRepository(PaymentDbContext db) : IPaymentRepository
{
    /// <summary>
    /// Thêm thực thể thanh toán vào DbSet và đẩy lệnh INSERT xuống PostgreSQL (US-032).
    /// </summary>
    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        // Gắn bản ghi vào DbSet Payments của EF Core
        await db.Payments.AddAsync(payment, cancellationToken);

        // Đẩy lệnh INSERT xuống cơ sở dữ liệu PostgreSQL
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Tìm kiếm giao dịch trong bảng Payments theo mã giao dịch nội bộ duy nhất (US-033).
    /// </summary>
    public async Task<Payment?> GetByCodeAsync(string paymentCode, CancellationToken cancellationToken)
    {
        return await db.Payments
            .FirstOrDefaultAsync(p => p.Code == paymentCode, cancellationToken);
    }

    /// <summary>
    /// Lưu đồng bộ các thay đổi trong 1 lần SaveChanges: Payment, CallbackLog, và OutboxMessage (US-033).
    /// </summary>
    public async Task SaveCallbackResultAsync(
        Payment? payment, 
        PaymentCallbackLog callbackLog, 
        OutboxMessage? outboxMessage, 
        CancellationToken cancellationToken)
    {
        // 1. Đánh dấu cập nhật Payment nếu bản ghi này tồn tại
        if (payment != null)
        {
            db.Payments.Update(payment);
        }

        // 2. Thêm bản ghi nhật ký callback vào bảng PaymentCallbackLogs
        await db.PaymentCallbackLogs.AddAsync(callbackLog, cancellationToken);

        // 3. Thêm bản tin Outbox vào bảng OutboxMessages nếu có sự kiện tích hợp phát sinh
        if (outboxMessage != null)
        {
            await db.Set<OutboxMessage>().AddAsync(outboxMessage, cancellationToken);
        }

        // 4. Xác nhận toàn bộ thay đổi xuống PostgreSQL pm_payment
        await db.SaveChangesAsync(cancellationToken);
    }
}

