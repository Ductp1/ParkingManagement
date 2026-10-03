using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Configurations;

internal sealed class RateCardConfiguration : IEntityTypeConfiguration<RateCard>
{
    public void Configure(EntityTypeBuilder<RateCard> e)
    {
        e.ToTable("RateCards", t =>
            t.HasCheckConstraint("CK_RateCards_Multipliers",
                "\"WeekendMultiplier\" > 0 AND \"HolidayMultiplier\" > 0 AND \"OversizedMultiplier\" >= 1 AND \"OverstayMultiplier\" >= 1"));
        e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        e.Property(x => x.WeekendMultiplier).HasPrecision(4, 2);
        e.Property(x => x.HolidayMultiplier).HasPrecision(4, 2);
        e.Property(x => x.OversizedMultiplier).HasPrecision(4, 2);
        e.Property(x => x.OverstayMultiplier).HasPrecision(4, 2);
        e.HasMany(x => x.Rules).WithOne(r => r.RateCard).HasForeignKey(r => r.RateCardId);
        e.HasIndex(x => new { x.ParkingLotId, x.IsActive, x.EffectiveFromUtc });
        e.HasIndex(x => x.OwnerProfileId);
    }
}
