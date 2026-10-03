using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Configurations;

internal sealed class SlotConfiguration : IEntityTypeConfiguration<Slot>
{
    public void Configure(EntityTypeBuilder<Slot> e)
    {
        e.ToTable("Slots");
        e.Property(x => x.Code).HasMaxLength(20).IsRequired();
        e.Property(x => x.RowVersion).IsConcurrencyToken();   // optimistic concurrency cho Slot State Machine (PostgreSQL: tự đóng dấu ở SaveChanges, không có rowversion tự sinh)

        e.HasIndex(x => new { x.FloorId, x.Code }).IsUnique();
        e.HasIndex(x => new { x.FloorId, x.GridX, x.GridY }).IsUnique();
        e.HasIndex(x => new { x.FloorId, x.State });
        e.HasIndex(x => x.DedicatedVehicleId).HasFilter("\"DedicatedVehicleId\" IS NOT NULL");
    }
}
