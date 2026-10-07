using BookingService.Application.Features.Bookings;
using BookingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Infrastructure.Persistence.Repositories;

public sealed class BookingRepository(BookingDbContext db) : IBookingRepository
{
    public Task<Booking?> GetEntityByCodeAsync(string code, CancellationToken cancellationToken = default)
        => db.Bookings
            .Include(b => b.StatusLogs)
            .Include(b => b.Modifications)
            .Include(b => b.PriceSnapshot)
            .FirstOrDefaultAsync(b => b.Code == code, cancellationToken);

    public async Task AddAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        await db.Bookings.AddAsync(booking, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}
