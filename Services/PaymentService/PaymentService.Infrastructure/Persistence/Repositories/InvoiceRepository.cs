using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Repositories;

/// <summary>
/// Cài đặt truy xuất dữ liệu hóa đơn điện tử bằng Entity Framework Core (US-036).
/// </summary>
public sealed class InvoiceRepository(PaymentDbContext dbContext) : IInvoiceRepository
{
    public async Task<Invoice?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Invoices
            .Include(i => i.Payment)
            .Include(i => i.Settlement)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<Invoice?> GetByPaymentIdAsync(int paymentId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Invoices
            .Include(i => i.Payment)
            .FirstOrDefaultAsync(i => i.PaymentId == paymentId, cancellationToken);
    }

    public async Task<Invoice?> GetByBookingIdAsync(int bookingId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Invoices
            .Include(i => i.Payment)
            .FirstOrDefaultAsync(i => i.Payment != null && i.Payment.BookingId == bookingId, cancellationToken);
    }

    public async Task<Invoice?> GetBySettlementIdAsync(int settlementId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Invoices
            .Include(i => i.Settlement)
            .FirstOrDefaultAsync(i => i.SettlementId == settlementId, cancellationToken);
    }

    public async Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default)
    {
        var normalized = invoiceNumber.Trim().ToUpperInvariant();
        return await dbContext.Invoices
            .Include(i => i.Payment)
            .Include(i => i.Settlement)
            .FirstOrDefaultAsync(i => i.InvoiceNumber == normalized, cancellationToken);
    }

    public async Task<Payment?> GetPaymentByIdAsync(int paymentId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Payments
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
    }

    public async Task<Settlement?> GetSettlementByIdAsync(int settlementId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Settlements
            .FirstOrDefaultAsync(s => s.Id == settlementId, cancellationToken);
    }

    public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        await dbContext.Invoices.AddAsync(invoice, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsForPaymentAsync(int paymentId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Invoices
            .AnyAsync(i => i.PaymentId == paymentId, cancellationToken);
    }

    public async Task<bool> ExistsForSettlementAsync(int settlementId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Invoices
            .AnyAsync(i => i.SettlementId == settlementId, cancellationToken);
    }
}

