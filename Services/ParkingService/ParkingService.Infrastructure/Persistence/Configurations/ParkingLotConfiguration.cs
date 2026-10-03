using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Configurations;

internal sealed class ParkingLotConfiguration : IEntityTypeConfiguration<ParkingLot>
{
    public void Configure(EntityTypeBuilder<ParkingLot> e)
    {
        e.ToTable("ParkingLots", t =>
        {
            t.HasCheckConstraint("CK_ParkingLots_Slots", "\"AvailableSlots\" >= 0 AND \"AvailableSlots\" <= \"TotalSlots\"");
            t.HasCheckConstraint("CK_ParkingLots_Latitude", "\"Latitude\" BETWEEN -90 AND 90");
            t.HasCheckConstraint("CK_ParkingLots_Longitude", "\"Longitude\" BETWEEN -180 AND 180");
        });
        e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        e.Property(x => x.Address).HasMaxLength(500).IsRequired();
        e.Property(x => x.City).HasMaxLength(100).IsRequired();
        e.Property(x => x.District).HasMaxLength(100);
        e.Property(x => x.Description).HasMaxLength(2000);
        e.Property(x => x.HotlinePhone).HasMaxLength(20);
        e.Property(x => x.CoverImageUrl).HasMaxLength(512);
        e.Property(x => x.RatingAverage).HasPrecision(3, 2);

        e.HasMany(x => x.Zones).WithOne(z => z.ParkingLot).HasForeignKey(z => z.ParkingLotId);
        e.HasOne(x => x.CapacityConfig).WithOne(c => c.ParkingLot).HasForeignKey<LotCapacityConfig>(c => c.ParkingLotId);

        e.HasIndex(x => x.OwnerProfileId);               // lọc theo tenant
        e.HasIndex(x => x.Status);
        e.HasIndex(x => new { x.Latitude, x.Longitude });
        e.HasIndex(x => new { x.City, x.District }); // tìm bãi theo khung tọa độ trước khi tính Haversine
    }
}
