using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using AdminService.Domain.Entities;

namespace AdminService.Infrastructure.Persistence.Configurations;

internal sealed class SanctionConfiguration : IEntityTypeConfiguration<Sanction>
{
    public void Configure(EntityTypeBuilder<Sanction> e)
    {
        e.ToTable("Sanctions");
        e.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        e.Property(x => x.EvidenceJson).IsMaxText();
        e.HasIndex(x => new { x.OwnerProfileId, x.Status });
        e.HasIndex(x => x.ParkingLotId).HasFilter("[ParkingLotId] IS NOT NULL");
    }
}
