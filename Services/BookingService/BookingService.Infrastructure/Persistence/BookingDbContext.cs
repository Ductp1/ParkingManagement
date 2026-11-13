using BookingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.ServiceDefaults.Persistence;

namespace BookingService.Infrastructure.Persistence;

/// <summary>Database riêng của BookingService: pm_booking. Booking 7 trạng thái, Price Lock snapshot, lịch sử trạng thái.</summary>
public sealed class BookingDbContext(DbContextOptions<BookingDbContext> options) : ServiceDbContext(options)
{
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<PriceSnapshot> PriceSnapshots => Set<PriceSnapshot>();
    public DbSet<BookingStatusLog> BookingStatusLogs => Set<BookingStatusLog>();
    public DbSet<BookingModification> BookingModifications => Set<BookingModification>();
    public DbSet<MonthlyPass> MonthlyPasses => Set<MonthlyPass>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // US-083: consumer PaymentSucceeded/Failed cần bảng InboxReceipts (idempotency theo EventId).
        modelBuilder.ConfigureInbox();
    }
}
