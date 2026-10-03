using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Configurations;

internal sealed class FloorConfiguration : IEntityTypeConfiguration<Floor>
{
    public void Configure(EntityTypeBuilder<Floor> e)
    {
        e.ToTable("Floors", t => t.HasCheckConstraint("CK_Floors_Grid", "\"GridColumns\" > 0 AND \"GridRows\" > 0"));
        e.Property(x => x.Name).HasMaxLength(50).IsRequired();
        e.HasMany(x => x.Slots).WithOne(s => s.Floor).HasForeignKey(s => s.FloorId);
        e.HasMany(x => x.LayoutVersions).WithOne(l => l.Floor).HasForeignKey(l => l.FloorId);
        e.HasIndex(x => new { x.ZoneId, x.Name }).IsUnique();
    }
}
