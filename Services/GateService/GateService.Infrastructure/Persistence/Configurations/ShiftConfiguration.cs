using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using GateService.Domain.Entities;

namespace GateService.Infrastructure.Persistence.Configurations;

internal sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> e)
    {
        e.ToTable("Shifts");
        e.Property(x => x.HandoverNote).HasMaxLength(1000);
        // Một nhân viên chỉ mở 1 ca tại một thời điểm.
        e.HasIndex(x => x.StaffUserId).IsUnique().HasFilter("\"Status\" = 'Open'");
        e.HasIndex(x => new { x.ParkingLotId, x.StartedAtUtc });
    }
}
