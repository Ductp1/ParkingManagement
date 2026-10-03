using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Configurations;

internal sealed class RateRuleConfiguration : IEntityTypeConfiguration<RateRule>
{
    public void Configure(EntityTypeBuilder<RateRule> e)
    {
        e.ToTable("RateRules", t =>
        {
            t.HasCheckConstraint("CK_RateRules_Range", "\"ToMinute\" IS NULL OR \"ToMinute\" > \"FromMinute\"");
            t.HasCheckConstraint("CK_RateRules_Block", "\"BlockMinutes\" > 0 AND \"PricePerBlock\" >= 0");
        });
        e.HasIndex(x => new { x.RateCardId, x.VehicleType, x.FromMinute }).IsUnique();
    }
}
