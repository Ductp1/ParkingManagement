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
    /// Thêm thực thể thanh toán vào DbSet và đẩy lệnh INSERT xuống PostgreSQL.
    /// </summary>
    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        // Gắn bản ghi vào DbSet Payments của EF Core
        await db.Payments.AddAsync(payment, cancellationToken);

        // Đẩy lệnh INSERT xuống cơ sở dữ liệu PostgreSQL
        await db.SaveChangesAsync(cancellationToken);
    }
}
