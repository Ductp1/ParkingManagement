using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.Configurations;

internal sealed class EmergencyAlertConfiguration : IEntityTypeConfiguration<EmergencyAlert>
{
    public void Configure(EntityTypeBuilder<EmergencyAlert> e)
    {
        e.ToTable("EmergencyAlerts");
        e.Property(x => x.BatchKey).HasMaxLength(100).IsRequired();
        e.Property(x => x.LotName).HasMaxLength(200).IsRequired();
        e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        e.HasIndex(x => x.BatchKey).IsUnique();
        e.HasIndex(x => new { x.ParkingLotId, x.CreatedAtUtc }); // danh sách đợt theo bãi (mới nhất trước)
    }
}
