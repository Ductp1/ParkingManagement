using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Configurations;

internal sealed class LotCapacityConfigConfiguration : IEntityTypeConfiguration<LotCapacityConfig>
{
    public void Configure(EntityTypeBuilder<LotCapacityConfig> e)
    {
        e.ToTable("LotCapacityConfigs", t =>
            t.HasCheckConstraint("CK_LotCapacity_Buffer", "\"WalkInBufferPercent\" BETWEEN 0 AND 100"));
        e.HasIndex(x => x.ParkingLotId).IsUnique();
    }
}
