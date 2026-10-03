using GateService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;

namespace GateService.Infrastructure.Persistence.Configurations;

internal sealed class GateDeviceConfiguration : IEntityTypeConfiguration<GateDevice>
{
    public void Configure(EntityTypeBuilder<GateDevice> e)
    {
        e.ToTable("GateDevices");
        e.Property(x => x.Code).HasMaxLength(40).IsRequired();
        e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        e.Property(x => x.Position).HasMaxLength(40);
        e.Property(x => x.FirmwareVersion).HasMaxLength(40);
        e.Property(x => x.OfflinePublicKey).IsMaxText();
        e.HasIndex(x => x.Code).IsUnique();
        e.HasIndex(x => new { x.ParkingLotId, x.Status });
    }
}
