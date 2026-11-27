using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GateService.Domain.Entities;

namespace GateService.Infrastructure.Persistence.Configurations;

internal sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> e)
    {
        e.ToTable("Shifts");
        e.Property(x => x.HandoverNote).HasMaxLength(1000);
        e.Property(x => x.DiscrepancyNote).HasMaxLength(1000);
        e.Property(x => x.Version).IsRowVersion();
        // Một người chỉ mở 1 ca tại một thời điểm.
        e.HasIndex(x => x.StaffUserId).IsUnique().HasFilter("\"Status\" = 'Open'");
        e.HasIndex(x => new { x.StaffUserId, x.ScheduledStartUtc });
        e.HasIndex(x => new { x.ParkingLotId, x.ScheduledStartUtc });
        e.HasIndex(x => x.HandoverToShiftId);
    }
}

internal sealed class ShiftTemplateConfiguration : IEntityTypeConfiguration<ShiftTemplate>
{
    public void Configure(EntityTypeBuilder<ShiftTemplate> e)
    {
        e.ToTable("ShiftTemplates");
        e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        e.HasIndex(x => new { x.ParkingLotId, x.Name }).IsUnique();
    }
}
