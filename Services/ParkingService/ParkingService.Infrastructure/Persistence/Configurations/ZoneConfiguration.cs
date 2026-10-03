using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Configurations;

internal sealed class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> e)
    {
        e.ToTable("Zones");
        e.Property(x => x.Code).HasMaxLength(20).IsRequired();
        e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        e.HasMany(x => x.Floors).WithOne(f => f.Zone).HasForeignKey(f => f.ZoneId);
        e.HasIndex(x => new { x.ParkingLotId, x.Code }).IsUnique();
    }
}
