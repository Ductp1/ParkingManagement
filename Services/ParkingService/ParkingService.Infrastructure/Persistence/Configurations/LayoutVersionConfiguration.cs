using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Configurations;

internal sealed class LayoutVersionConfiguration : IEntityTypeConfiguration<LayoutVersion>
{
    public void Configure(EntityTypeBuilder<LayoutVersion> e)
    {
        e.ToTable("LayoutVersions");
        e.Property(x => x.LayoutJson).IsMaxText().IsRequired();
        e.HasIndex(x => new { x.FloorId, x.VersionNo }).IsUnique();
    }
}
