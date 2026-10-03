using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using BookingService.Domain.Entities;

namespace BookingService.Infrastructure.Persistence.Configurations;

internal sealed class BookingStatusLogConfiguration : IEntityTypeConfiguration<BookingStatusLog>
{
    public void Configure(EntityTypeBuilder<BookingStatusLog> e)
    {
        e.ToTable("BookingStatusLogs");
        e.HasIndex(x => new { x.BookingId, x.CreatedAtUtc });
    }
}
