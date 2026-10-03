using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Features;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Queries;

public sealed class RateCardRepository(PaymentDbContext db) : IRateCardRepository
{
    public Task<RateCard?> GetActiveAsync(int parkingLotId, DateTime atUtc, CancellationToken cancellationToken)
        => db.RateCards.AsNoTracking()
            .Include(c => c.Rules)
            .Where(c => c.ParkingLotId == parkingLotId && c.IsActive
                     && c.EffectiveFromUtc <= atUtc && (c.EffectiveToUtc == null || c.EffectiveToUtc > atUtc))
            .OrderByDescending(c => c.EffectiveFromUtc)
            .FirstOrDefaultAsync(cancellationToken);
}

public sealed class PaymentQueries(PaymentDbContext db) : IPaymentQueries
{
    public async Task<IReadOnlyList<PaymentDto>> ListByBookingAsync(int bookingId, CancellationToken cancellationToken)
        => await db.Payments.AsNoTracking()
            .Where(p => p.BookingId == bookingId)
            .OrderBy(p => p.CreatedAtUtc)
            .Select(p => new PaymentDto(p.Id, p.Code, p.BookingId, p.BookingCode, p.Method.ToString(), p.Status.ToString(),
                p.Amount, p.RefundedAmount, p.ProviderTransactionId, p.PaidAtUtc,
                p.Invoice == null ? null : p.Invoice.InvoiceNumber))
            .ToListAsync(cancellationToken);
}
