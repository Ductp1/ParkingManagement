using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Features;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Queries;

/// <summary>Kho truy vấn dữ liệu biểu giá bãi đỗ xe.</summary>
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

/// <summary>Kho truy vấn lịch sử và giao dịch thanh toán.</summary>
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

/// <summary>Kho truy vấn dữ liệu ngày nghỉ lễ quốc gia và địa phương phục vụ áp hệ số US-071.</summary>
public sealed class HolidayRepository(PaymentDbContext db) : IHolidayRepository
{
    /// <summary>Lấy danh sách các ngày nghỉ lễ nằm trong khoảng thời gian đỗ xe theo lịch Việt Nam.</summary>
    public async Task<IReadOnlyCollection<DateOnly>> GetHolidaysInRangeAsync(
        DateOnly fromDate, 
        DateOnly toDate, 
        CancellationToken cancellationToken)
    {
        // Tắt theo dõi trạng thái vì đây là thao tác chỉ đọc dữ liệu phục vụ tính toán bảng giá
        return await db.Holidays
            .AsNoTracking()
            .Where(h => h.Date >= fromDate && h.Date <= toDate)
            .Select(h => h.Date)
            .ToListAsync(cancellationToken);
    }
}
