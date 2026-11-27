using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Features;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence;

/// <summary>Cài đặt port ghi giao dịch thanh toán (UC-22): idempotent theo IdempotencyKey, đã có unique index.</summary>
public sealed class PaymentWriter(PaymentDbContext db) : IPaymentWriter
{
    public Task<Payment?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
        => db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
        => await db.Payments.AddAsync(payment, cancellationToken);

    public async Task SaveAsync(CancellationToken cancellationToken)
        => await db.SaveChangesAsync(cancellationToken);
}
