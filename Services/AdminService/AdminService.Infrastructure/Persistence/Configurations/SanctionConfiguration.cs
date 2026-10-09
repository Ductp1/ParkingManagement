using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;

namespace AdminService.Infrastructure.Persistence.Configurations;

internal sealed class SanctionConfiguration : IEntityTypeConfiguration<Sanction>
{
    public void Configure(EntityTypeBuilder<Sanction> e)
    {
        e.ToTable("Sanctions");
        e.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        e.Property(x => x.EvidenceJson).IsMaxText();
        // Chế tài có trước US-096 không cần đồng bộ sang UserService.
        e.Property(x => x.UserServiceSyncStatus).HasDefaultValue(SanctionSyncStatus.NotRequired);
        e.HasIndex(x => new { x.OwnerProfileId, x.Status });
        e.HasIndex(x => x.ParkingLotId).HasFilter("\"ParkingLotId\" IS NOT NULL");
    }
}
