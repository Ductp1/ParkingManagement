using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;

namespace AdminService.Infrastructure.Persistence.Configurations;

internal sealed class RiskFlagConfiguration : IEntityTypeConfiguration<RiskFlag>
{
    public void Configure(EntityTypeBuilder<RiskFlag> e)
    {
        e.ToTable("RiskFlags");
        e.Property(x => x.RuleCode).HasMaxLength(60).IsRequired();
        e.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        e.Property(x => x.EvidenceJson).IsMaxText();
        e.Property(x => x.ResolutionNote).HasMaxLength(1000);
        e.HasOne(x => x.Sanction).WithMany().HasForeignKey(x => x.SanctionId);
        e.HasIndex(x => new { x.Status, x.Severity, x.DueAtUtc });           // hàng đợi xử lý của Admin
        e.HasIndex(x => new { x.SubjectType, x.SubjectId });
    }
}
