using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using BookingService.Domain.Entities;

namespace BookingService.Infrastructure.Persistence.Configurations;

internal sealed class PriceSnapshotConfiguration : IEntityTypeConfiguration<PriceSnapshot>
{
    public void Configure(EntityTypeBuilder<PriceSnapshot> e)
    {
        e.ToTable("PriceSnapshots");
        e.Property(x => x.RateCardJson).IsMaxText().IsRequired();
        e.Property(x => x.OverstayMultiplier).HasPrecision(4, 2);
        e.HasIndex(x => x.BookingId).IsUnique();
    }
}
