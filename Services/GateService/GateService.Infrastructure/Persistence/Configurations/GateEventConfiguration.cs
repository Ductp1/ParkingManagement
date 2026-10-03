using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using GateService.Domain.Entities;

namespace GateService.Infrastructure.Persistence.Configurations;

internal sealed class GateEventConfiguration : IEntityTypeConfiguration<GateEvent>
{
    public void Configure(EntityTypeBuilder<GateEvent> e)
    {
        e.ToTable("GateEvents");
        e.Property(x => x.PlateNumber).HasMaxLength(15);
        e.Property(x => x.ImagePath).HasMaxLength(512);
        e.Property(x => x.OcrRaw).HasMaxLength(100);
        e.Property(x => x.Note).HasMaxLength(1000);
        e.HasOne(x => x.Shift).WithMany(s => s.Events).HasForeignKey(x => x.ShiftId);
        e.HasIndex(x => new { x.ParkingLotId, x.CreatedAtUtc });
        e.HasIndex(x => new { x.EventType, x.CreatedAtUtc });
    }
}
