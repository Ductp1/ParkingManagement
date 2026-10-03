using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Enums;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence.Configurations;

internal sealed class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> e)
    {
        e.ToTable("Promotions", t => t.HasCheckConstraint("CK_Promotions_Period", "\"EndsAtUtc\" > \"StartsAtUtc\""));
        e.Property(x => x.Code).HasMaxLength(30).IsRequired();
        e.Property(x => x.Name).HasMaxLength(150).IsRequired();
        e.HasIndex(x => x.Code).IsUnique();
        e.HasIndex(x => new { x.ParkingLotId, x.IsActive });
    }
}
